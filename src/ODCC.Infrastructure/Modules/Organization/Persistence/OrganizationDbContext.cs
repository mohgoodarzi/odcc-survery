using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Organization.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول سازمان.
/// </summary>
public class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<OrgUnit>(b =>
        {
            b.ConfigureBase("org_units", provider);
            b.Property(u => u.Code).HasMaxLength(64).IsRequired();
            b.Property(u => u.Name).HasMaxLength(256).IsRequired();
            b.Property(u => u.Path).HasMaxLength(1024).IsRequired();
            b.Property(u => u.Type).HasConversion<int>();
            b.HasIndex(u => u.Code).IsUnique();
            b.HasIndex(u => u.Path).IsUnique();
            b.HasIndex(u => u.ParentId);
            b.HasIndex(u => u.IsActive);
        });

        modelBuilder.Entity<Position>(b =>
        {
            b.ConfigureBase("positions", provider);
            b.Property(p => p.Code).HasMaxLength(64).IsRequired();
            b.Property(p => p.Title).HasMaxLength(256).IsRequired();
            b.Property(p => p.Description).HasMaxLength(1024);
            b.HasIndex(p => p.Code).IsUnique();
            b.HasIndex(p => p.OrgUnitId);
            b.HasIndex(p => p.ReportsToPositionId);
        });

        modelBuilder.Entity<Employee>(b =>
        {
            b.ConfigureBase("employees", provider);
            b.Property(e => e.EmployeeCode).HasMaxLength(32).IsRequired();
            b.Property(e => e.NationalCode).HasMaxLength(10);
            b.Property(e => e.FirstName).HasMaxLength(128).IsRequired();
            b.Property(e => e.LastName).HasMaxLength(128).IsRequired();
            b.Property(e => e.FatherName).HasMaxLength(128);
            b.Property(e => e.WorkEmail).HasMaxLength(256);
            b.Property(e => e.InternalPhone).HasMaxLength(32);
            b.Property(e => e.Status).HasConversion<int>();
            b.HasIndex(e => e.EmployeeCode).IsUnique();
            b.HasIndex(e => e.UserId).IsUnique();
            b.HasIndex(e => e.OrgUnitId);
            b.HasIndex(e => e.ManagerId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
