// Copyright © Erickson Lopez. MIT License.
using Microsoft.EntityFrameworkCore;

namespace EricksonLopez.Auditing.EntityFrameworkCore;

/// <summary>Provides extension methods for configuring audit record entity mappings on <see cref="ModelBuilder"/>.</summary>
public static class AuditDbContextModelBuilderExtensions
{
    /// <summary>Configures the entity mapping, indexes, and column constraints for <see cref="AuditRecordEntity"/>.</summary>
    /// <param name="modelBuilder">The model builder being configured.</param>
    /// <param name="tableName">The database table name for audit records.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for fluent chaining.</returns>
    public static ModelBuilder ApplyAuditRecordConfiguration(this ModelBuilder modelBuilder, string tableName = "audit_records")
    {
        modelBuilder.Entity<AuditRecordEntity>(b =>
        {
            b.ToTable(tableName);
            b.HasKey(e => e.Id);

            b.Property(e => e.TenantId).HasMaxLength(128).IsRequired();
            b.Property(e => e.Source).HasMaxLength(256).IsRequired();
            b.Property(e => e.ActionCode).HasMaxLength(128).IsRequired();
            b.Property(e => e.ResourceType).HasMaxLength(256).IsRequired();
            b.Property(e => e.ResourceId).HasMaxLength(256).IsRequired();
            b.Property(e => e.ActorId).HasMaxLength(256).IsRequired();
            b.Property(e => e.ActorName).HasMaxLength(256);
            b.Property(e => e.CorrelationId).HasMaxLength(128);
            b.Property(e => e.CausationId).HasMaxLength(128);
            b.Property(e => e.ErrorCode).HasMaxLength(128);
            b.Property(e => e.IntegrityHash).HasMaxLength(256);
            b.Property(e => e.PreviousHash).HasMaxLength(256);

            b.HasIndex(e => new { e.TenantId, e.OccurredAt, e.Id });
            b.HasIndex(e => new { e.TenantId, e.CorrelationId });
            b.HasIndex(e => new { e.TenantId, e.ResourceType, e.ResourceId });
        });

        return modelBuilder;
    }
}
