using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Entities;
using ODCC.Domain.Modules.Analytics.Enums;

namespace ODCC.Infrastructure.Modules.Analytics.Services;

/// <summary>
/// سرویس مدیریت بنچمارک‌ها (اهداف مرجع شاخص‌ها).
///
/// بنچمارک‌ها فقط یک عدد مرجع هستند و هیچ داده‌ی پاسخ‌گو ندارند. دامنه‌ی یک
/// بنچمارک می‌تواند «تمام شرکت» یا یک واحد سازمانی مشخص باشد.
/// </summary>
public sealed class BenchmarkService(
    IBenchmarkRepository benchmarkRepository,
    IOrgUnitRepository orgUnitRepository,
    IAnalyticsUnitOfWork unitOfWork) : IBenchmarkService
{
    private readonly IBenchmarkRepository _benchmarkRepository = benchmarkRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IAnalyticsUnitOfWork _unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async Task<PagedResult<BenchmarkDto>> SearchAsync(
        string? searchText, bool includeInactive, CancellationToken ct = default)
    {
        var benchmarks = await _benchmarkRepository.SearchAsync(searchText, includeInactive, ct);

        var items = benchmarks.Select(ToDto).ToList();

        return new PagedResult<BenchmarkDto>
        {
            Items = items,
            TotalCount = items.Count,
            Page = 1,
            PageSize = items.Count
        };
    }

    /// <inheritdoc/>
    public async Task<Result<BenchmarkDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var benchmark = await _benchmarkRepository.GetByIdAsync(id, ct);
        if (benchmark is null)
        {
            return Result.Failure<BenchmarkDto>("benchmark_not_found", "بنچمارک یافت نشد.");
        }

        return Result.Success(ToDto(benchmark));
    }

    /// <inheritdoc/>
    public async Task<Result<BenchmarkDto>> CreateAsync(
        SaveBenchmarkRequest request, CancellationToken ct = default)
    {
        var orgUnitPath = await ResolveOrgUnitPathAsync(request, ct);

        var benchmark = new Benchmark
        {
            Name = request.Name,
            Metric = request.Metric,
            TargetValue = request.TargetValue,
            IsCompanyWide = request.IsCompanyWide,
            OrgUnitId = orgUnitPath.OrgUnitId,
            OrgUnitPath = orgUnitPath.Path,
            Description = request.Description,
            IsActive = true
        };

        await _benchmarkRepository.AddAsync(benchmark, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(benchmark));
    }

    /// <inheritdoc/>
    public async Task<Result<BenchmarkDto>> UpdateAsync(
        Guid id, SaveBenchmarkRequest request, CancellationToken ct = default)
    {
        var benchmark = await _benchmarkRepository.GetByIdAsync(id, ct);
        if (benchmark is null)
        {
            return Result.Failure<BenchmarkDto>("benchmark_not_found", "بنچمارک یافت نشد.");
        }

        var orgUnitPath = await ResolveOrgUnitPathAsync(request, ct);

        benchmark.Update(
            request.Name,
            request.Metric,
            request.TargetValue,
            request.IsCompanyWide,
            orgUnitPath.OrgUnitId,
            orgUnitPath.Path,
            request.Description);

        _benchmarkRepository.Update(benchmark);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(benchmark));
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var benchmark = await _benchmarkRepository.GetByIdAsync(id, ct);
        if (benchmark is null)
        {
            return Result.Failure("benchmark_not_found", "بنچمارک یافت نشد.");
        }

        // حذف نرم: ردیف برای تاریخچه و پیگیری ممیزی نگه داشته می‌شود.
        benchmark.Deactivate();

        _benchmarkRepository.Update(benchmark);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>حل مسیر سازمانی بنچمارک (در صورت غیرسراسری بودن).</summary>
    private async Task<(Guid? OrgUnitId, string? Path)> ResolveOrgUnitPathAsync(
        SaveBenchmarkRequest request, CancellationToken ct)
    {
        if (request.IsCompanyWide || request.OrgUnitId is null || request.OrgUnitId == Guid.Empty)
        {
            return (null, null);
        }

        var path = await _orgUnitRepository.GetPathAsync(request.OrgUnitId.Value, ct);

        return (request.OrgUnitId, path);
    }

    /// <summary>تبدیل موجودیت به DTO.</summary>
    private static BenchmarkDto ToDto(Benchmark benchmark)
    {
        return new BenchmarkDto
        {
            Id = benchmark.Id,
            Name = benchmark.Name,
            Metric = benchmark.Metric,
            TargetValue = benchmark.TargetValue,
            IsCompanyWide = benchmark.IsCompanyWide,
            OrgUnitId = benchmark.OrgUnitId,
            OrgUnitPath = benchmark.OrgUnitPath,
            Description = benchmark.Description,
            IsActive = benchmark.IsActive,
            CreatedAt = benchmark.CreatedAt
        };
    }
}
