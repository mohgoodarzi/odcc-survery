using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Persistence;

namespace ODCC.Infrastructure.Modules.Identity;

/// <summary>
/// صدور توکن JWT و توکن تازه‌سازی.
///
/// توکن تازه‌سازی به‌صورت هش SHA-256 در پایگاه داده ذخیره می‌شود؛
/// توکن خام فقط یک‌بار به مشتری برگردانده می‌شود.
///
/// <b>مرز تراکنش:</b> این سرویس توکن تازه‌سازی را فقط به DbContext اضافه می‌کند
/// (Add) و هرگز مستقیماً <c>SaveChanges</c> نمی‌زند. ذخیره‌سازی در پایان عملیات
/// توسط <c>IIdentityUnitOfWork</c> انجام می‌شود تا صدور توکن و سایر تغییرات
/// در یک تراکنش واحد commit شوند. اگر سرویس فراخوان مستقیماً SaveChanges نزند،
/// توکن ذخیره نخواهد شد.
/// </summary>
public sealed class JwtTokenService(
    IdentityDbContext dbContext,
    IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly IdentityDbContext _dbContext = dbContext;

    public Task<IssuedTokens> IssueAsync(TokenIssueContext context, CancellationToken ct = default)
    {
        var accessToken = GenerateAccessToken(context);
        var (refreshTokenPlain, refreshTokenHash) = GenerateRefreshToken();

        // در چرخش، خانواده‌ی توکن قبلی حفظ می‌شود تا ابطال خانواده در صورت
        // استفاده‌ی مجدد از توکن مصرف‌شده، کل زنجیره را باطل کند.
        var familyId = context.FamilyId ?? Guid.CreateVersion7();

        var refreshToken = new RefreshToken
        {
            UserId = context.UserId,
            TokenHash = refreshTokenHash,
            FamilyId = familyId,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshExpirationDays),
            DeviceInfo = context.DeviceInfo,
            CreatedByIp = context.ClientIp
        };

        // فقط tracking؛ commit با IUnitOfWork انجام می‌شود.
        _dbContext.RefreshTokens.Add(refreshToken);

        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessExpirationMinutes);

        var response = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenPlain,
            TokenType = "Bearer",
            ExpiresAt = expiresAt
        };

        return Task.FromResult(new IssuedTokens(response, refreshTokenHash, familyId));
    }

    public ClaimsPrincipal? ValidateAccessToken(string accessToken, bool validateLifetime = true)
    {
        try
        {
            var validationParameters = GetValidationParameters(validateLifetime);
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(accessToken, validationParameters, out _);
        }
        catch (Exception)
        {
            // توکن نامعتبر یا منقضی. لاگ کردن جزئیات توکن امن نیست؛ فقط null برمی‌گردانیم.
            return null;
        }
    }

    /// <summary>پارامترهای اعتبارسنجی توکن. دو بار استفاده می‌شود: اعتبارسنجی کامل و در زمان چرخش.</summary>
    internal TokenValidationParameters GetValidationParameters(bool validateLifetime) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = validateLifetime,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _options.Issuer,
        ValidAudience = _options.Audience,
        IssuerSigningKey = GetSigningKey(),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    private string GenerateAccessToken(TokenIssueContext context)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, context.UserId.ToString()),
            new(JwtRegisteredClaimNames.Name, context.UserName),
            new(JwtRegisteredClaimNames.GivenName, context.DisplayName),
            new(JwtRegisteredClaimNames.Email, context.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            // کلیمهای دامنه‌ی سازمانی برای مجوزدهی سطح داده
            new("org_unit", context.OrgUnitId?.ToString() ?? string.Empty),
            new("data_scope", context.DataScope.ToString("D"))
        };

        foreach (var role in context.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in context.Permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(Permissions.ClaimType, permission));
        }

        var key = GetSigningKey();
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// تولید توکن تازه‌سازی: ۳۲ بایت تصادفی به base64url.
    /// فقط هش آن ذخیره می‌شود.
    /// </summary>
    private static (string plainToken, string tokenHash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plainToken = Base64UrlEncoder.Encode(bytes);
        var hash = HashToken(plainToken);
        return (plainToken, hash);
    }

    /// <summary>هش کردن توکن تازه‌سازی با SHA-256.</summary>
    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        return HashToken(refreshToken);
    }

    /// <summary>هش کردن توکن تازه‌سازی با SHA-256.</summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Base64UrlEncoder.Encode(bytes);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        if (_options.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "کلید امضای JWT حداقل باید ۳۲ کاراکتر باشد. آن را با متغیر محیطی Jwt__Secret " +
                "یا user secret تامین کنید. هرگز کلید واقعی را در مخزن کد قرار ندهید.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
    }
}
