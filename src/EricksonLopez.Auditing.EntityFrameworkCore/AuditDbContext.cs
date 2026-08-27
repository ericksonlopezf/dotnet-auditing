// Copyright © Erickson Lopez. MIT License.
using Microsoft.EntityFrameworkCore;

namespace EricksonLopez.Auditing.EntityFrameworkCore;

/// <summary>Represents the Entity Framework Core database context for persisting and querying audit records.</summary>
public class AuditDbContext : DbContext
{
    /// <summary>Gets the entity set of persisted audit records.</summary>
    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    /// <summary>Initializes a new instance of the <see cref="AuditDbContext"/> class with strongly-typed options.</summary>
    /// <param name="options">The options to be used by this <see cref="DbContext"/>.</param>
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuditDbContext"/> class for derived context types.</summary>
    /// <param name="options">The generic options to be used by this <see cref="DbContext"/>.</param>
    protected AuditDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAuditRecordConfiguration();
    }
}
