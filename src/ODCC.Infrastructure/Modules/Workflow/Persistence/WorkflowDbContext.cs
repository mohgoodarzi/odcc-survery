using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Workflow.Entities;
using ODCC.Domain.Modules.Workflow.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Workflow.Persistence;

/// <summary>
/// DbContext ماژول گردش کار.
///
/// جداول این ماژول مستقل از سایر ماژول‌هاست. این ماژول فقط شناسه‌ی موجودیت
/// هدف را نگه می‌دارد و هرگز به DbContext ماژول دیگر دسترسی مستقیم ندارد.
/// </summary>
public class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    /// <summary>تعاریف گردش کار.</summary>
    public DbSet<WorkflowDefinition> Workflows => Set<WorkflowDefinition>();

    /// <summary>وضعیت‌های تعاریف.</summary>
    public DbSet<WorkflowState> WorkflowStates => Set<WorkflowState>();

    /// <summary>گذارهای تعاریف.</summary>
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

    /// <summary>نمونه‌های در حال اجرا.</summary>
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    /// <summary>درخواست‌های تأیید.</summary>
    public DbSet<WorkflowApprovalRequest> WorkflowApprovalRequests => Set<WorkflowApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<WorkflowDefinition>(b =>
        {
            b.ConfigureBase("workflows", provider);

            b.Property(w => w.Name).HasMaxLength(300).IsRequired();
            b.Property(w => w.Code).HasMaxLength(128).IsRequired();
            b.Property(w => w.Description).HasMaxLength(2000);
            b.Property(w => w.Status).HasConversion<int>();
            b.Property(w => w.EntityType).HasConversion<int>();
            b.Property(w => w.CreatedByUserName).HasMaxLength(256);

            // کد یکتا: فقط تعاریف غیر بایگانی‌شده. فیلترشده تا چندین ردیف
            // بایگانی‌شده بتوانند کد تکراری داشته باشند (داده‌ی تاریخی).
            b.HasIndex(w => w.Code)
                .IsUnique()
                .HasFilter($"[{nameof(WorkflowDefinition.Code)}] IS NOT NULL AND [{nameof(WorkflowDefinition.Status)}] <> {(int)WorkflowStatus.Archived}");

            b.HasIndex(w => w.Status);
            b.HasIndex(w => new { w.EntityType, w.Status });

            b.HasMany(w => w.States)
                .WithOne()
                .HasForeignKey(s => s.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(w => w.Transitions)
                .WithOne()
                .HasForeignKey(t => t.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowState>(b =>
        {
            b.ConfigureBase("workflow_states", provider);

            b.Property(s => s.Code).HasMaxLength(64).IsRequired();
            b.Property(s => s.Name).HasMaxLength(200).IsRequired();

            // کد یکتا درwithin یک تعریف.
            b.HasIndex(s => new { s.WorkflowId, s.Code }).IsUnique();
            b.HasIndex(s => s.WorkflowId);
        });

        modelBuilder.Entity<WorkflowTransition>(b =>
        {
            b.ConfigureBase("workflow_transitions", provider);

            b.Property(t => t.Code).HasMaxLength(64).IsRequired();
            b.Property(t => t.Name).HasMaxLength(200).IsRequired();
            b.Property(t => t.ApproverPermission).HasMaxLength(256);

            b.HasIndex(t => new { t.WorkflowId, t.Code }).IsUnique();
            b.HasIndex(t => t.WorkflowId);
            b.HasIndex(t => t.FromStateId);
            b.HasIndex(t => t.ToStateId);

            b.HasOne(t => t.FromState)
                .WithMany()
                .HasForeignKey(t => t.FromStateId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.ToState)
                .WithMany()
                .HasForeignKey(t => t.ToStateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowInstance>(b =>
        {
            b.ConfigureBase("workflow_instances", provider);

            b.Property(i => i.WorkflowCode).HasMaxLength(128).IsRequired();
            b.Property(i => i.CurrentStateCode).HasMaxLength(64).IsRequired();
            b.Property(i => i.StartedByUserName).HasMaxLength(256);
            b.Property(i => i.Status).HasConversion<int>();
            b.Property(i => i.EntityType).HasConversion<int>();
            b.Property(i => i.ContextJson).HasMaxLength(4000);
            b.Property(i => i.OrgUnitPath).HasMaxLength(512);

            b.Property(i => i.StartedAt).HasColumnType("datetime2");
            b.Property(i => i.CompletedAt).HasColumnType("datetime2");
            b.Property(i => i.CancelledAt).HasColumnType("datetime2");

            // یک نمونه‌ی فعال به ازای هر موجودیت — فیلترشده تا نمونه‌های
            // تمام‌شده تداخل ایجاد نکنند.
            b.HasIndex(i => new { i.EntityType, i.EntityId, i.Status })
                .IsUnique()
                .HasFilter($"[{nameof(WorkflowInstance.Status)}] = {(int)WorkflowInstanceState.Running}");

            b.HasIndex(i => new { i.EntityType, i.EntityId });
            b.HasIndex(i => i.WorkflowId);
            b.HasIndex(i => i.Status);
            b.HasIndex(i => i.StartedAt);
            // شاخص مسیر سازمانی برای فیلتر سریع دامنه‌ی دسترسی.
            b.HasIndex(i => i.OrgUnitPath);
        });

        modelBuilder.Entity<WorkflowApprovalRequest>(b =>
        {
            b.ConfigureBase("workflow_approval_requests", provider);

            b.Property(a => a.TransitionCode).HasMaxLength(64).IsRequired();
            b.Property(a => a.FromStateCode).HasMaxLength(64).IsRequired();
            b.Property(a => a.ToStateCode).HasMaxLength(64).IsRequired();
            b.Property(a => a.ApproverPermission).HasMaxLength(256);
            b.Property(a => a.Status).HasConversion<int>();
            b.Property(a => a.DecidedByUserName).HasMaxLength(256);
            b.Property(a => a.DecisionNote).HasMaxLength(1000);

            b.Property(a => a.RequestedAt).HasColumnType("datetime2");
            b.Property(a => a.DecidedAt).HasColumnType("datetime2");
            b.Property(a => a.ExpiresAt).HasColumnType("datetime2");

            // یک درخواست در انتظار به ازای هر نمونه — جلوگیری از درخواست مضاعف.
            b.HasIndex(a => new { a.InstanceId, a.Status })
                .IsUnique()
                .HasFilter($"[{nameof(WorkflowApprovalRequest.Status)}] = {(int)ApprovalStatus.Pending}");

            b.HasIndex(a => a.InstanceId);
            b.HasIndex(a => a.Status);
            b.HasIndex(a => new { a.Status, a.ExpiresAt }); // زمان‌بند انقضا
        });

        base.OnModelCreating(modelBuilder);
    }
}
