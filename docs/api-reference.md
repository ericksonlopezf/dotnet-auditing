# Public API Reference: EricksonLopez.Auditing

Comprehensive Microsoft Learn-style API specification for all public types, methods, options, and extension methods across the 12 packages in the `EricksonLopez.Auditing` ecosystem.

---

## Table of Contents

1. [EricksonLopez.Auditing (Core)](#1-namespace-ericksonlopezauditing)
2. [EricksonLopez.Auditing.Abstractions (Package)](#2-package-ericksonlopezauditingabstractions--namespace-ericksonlopezauditing)
3. [EricksonLopez.Auditing.Testing](#3-namespace-ericksonlopezauditingtesting)
4. [EricksonLopez.Auditing.Dapper](#4-namespace-ericksonlopezauditingdapper)
5. [EricksonLopez.Auditing.PostgreSql](#5-namespace-ericksonlopezauditingpostgresql)
6. [EricksonLopez.Auditing.SqlServer](#6-namespace-ericksonlopezauditingsqlserver)
7. [EricksonLopez.Auditing.Sqlite](#7-namespace-ericksonlopezauditingsqlite)
8. [EricksonLopez.Auditing.MySql](#8-namespace-ericksonlopezauditingmysql)
9. [EricksonLopez.Auditing.Oracle](#9-namespace-ericksonlopezauditingoracle)
10. [EricksonLopez.Auditing.MongoDb](#10-namespace-ericksonlopezauditingmongodb)
11. [EricksonLopez.Auditing.EntityFrameworkCore](#11-namespace-ericksonlopezauditingentityframeworkcore)
12. [EricksonLopez.Auditing.OpenTelemetry](#12-namespace-ericksonlopezauditingopentelemetry)

---

## 1. Namespace: `EricksonLopez.Auditing`

### Classes & Types

#### `AuditId` (Static Class)
Provides monotonic, timestamp-ordered UUID generation according to RFC 9562 (UUIDv7).
```csharp
public static class AuditId
{
    public static Guid NewId();
}
```

#### `AuditScope` (Sealed Class, `IDisposable`)
Manages ambient correlation and metadata enrichment via `AsyncLocal<T>` with nested hierarchy restoration.
```csharp
public sealed class AuditScope : IDisposable
{
    public static AuditScope? Current { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    
    public static AuditScope Begin(IReadOnlyDictionary<string, string>? initialMetadata = null);
    public AuditScope WithMetadata(string key, string value);
    public void Dispose();
}
```

#### `AuditConfiguration` (Sealed Class)
Runtime configuration options for auditing pipeline execution.
```csharp
public sealed class AuditConfiguration
{
    public AuditFailureBehavior DefaultFailureBehavior { get; set; } = AuditFailureBehavior.FailClosed;
    public HashSet<string> CriticalActionCodes { get; }
    public HashSet<string> GlobalFieldDenylist { get; }
    public bool EnableIntegrityChain { get; set; }
    public int BatchChannelCapacity { get; set; } = 1000;
    public int BatchSize { get; set; } = 100;
    public TimeSpan BatchFlushInterval { get; set; } = TimeSpan.FromSeconds(5);
}
```

#### `AuditFailureBehavior` (Enum)
```csharp
public enum AuditFailureBehavior
{
    FailClosed = 1, // Exceptions in audit store propagate and fail the caller
    FailOpen = 2,   // Audit store failures are logged/swallowed, allowing caller to continue
    Deferred = 3    // Events are buffered locally for deferred background retries
}
```

#### `AuditFieldSensitivity` (Enum)
```csharp
public enum AuditFieldSensitivity
{
    Include = 0,
    Exclude = 1,
    Redact = 2,
    Hash = 3
}
```

#### `AuditSensitivityPipeline` (Sealed Class)
Sanitizes changes against denylists, redaction markers, and cryptographic hashing rules.
```csharp
public sealed class AuditSensitivityPipeline
{
    public AuditSensitivityPipeline(AuditConfiguration config);
    public IReadOnlyList<AuditChange>? Apply(IReadOnlyList<AuditChange>? changes);
    public static string HashValue(string value); // Lowercase SHA-256 hex string
}
```

#### `AuditingServiceCollectionExtensions` (Static Class)
```csharp
public static class AuditingServiceCollectionExtensions
{
    public static IAuditBuilder AddAuditing(
        this IServiceCollection services,
        Action<AuditConfiguration>? configure = null);
}
```

---

## 2. Package: `EricksonLopez.Auditing.Abstractions` — Namespace: `EricksonLopez.Auditing`

> **Note:** All types in this package are declared in the `EricksonLopez.Auditing` namespace, not `EricksonLopez.Auditing.Abstractions`. Add `using EricksonLopez.Auditing;` to access them.

### Canonical Domain Model

#### `AuditRecord` (Sealed Record)
Represents canonical, immutable evidence of an audited event.
```csharp
public sealed record AuditRecord
{
    public required Guid Id { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required AuditActor Actor { get; init; }
    public required AuditAction Action { get; init; }
    public required AuditResource Resource { get; init; }
    public required AuditOutcome Outcome { get; init; }
    public required AuditContext Context { get; init; }
    public IReadOnlyList<AuditChange>? Changes { get; init; }
    public string? ErrorCode { get; init; }
    public string? IntegrityHash { get; init; }
    public string? PreviousHash { get; init; }
}
```

#### `AuditActor` (Sealed Record) & `AuditActorType` (Enum)
```csharp
public enum AuditActorType : byte
{
    User = 1,
    SystemProcess = 2,  // Non-interactive system process or background job
    Service = 3,        // Named service or microservice identity
    ScheduledJob = 4,
    Integration = 5,
    Anonymous = 6
}

public sealed record AuditActor(
    AuditActorType Type,
    string Id,
    string? DisplayName = null)
{
    public static readonly AuditActor Anonymous;  // AuditActorType.Anonymous, "anonymous"
    public static readonly AuditActor System;     // AuditActorType.SystemProcess, "system"
}
```

#### `AuditAction` (Readonly Record Struct)
Extensible value object (non-enum) representing the business operation.
```csharp
public readonly record struct AuditAction(string Code)
{
    public static readonly AuditAction Create;
    public static readonly AuditAction Read;
    public static readonly AuditAction Update;
    public static readonly AuditAction Delete;
    public static readonly AuditAction Approve;
    public static readonly AuditAction Reject;
    public static readonly AuditAction Login;
    public static readonly AuditAction Logout;
    public static readonly AuditAction Export;
    public static readonly AuditAction Download;
    public static readonly AuditAction Send;
    public static readonly AuditAction Cancel;
    public static readonly AuditAction Restore;
    public static readonly AuditAction GrantPermission;
    public static readonly AuditAction RevokePermission;
}
```

#### `AuditResource` (Sealed Record)
```csharp
public sealed record AuditResource(
    string Type,
    string Id,
    string? AggregateType = null,
    string? AggregateId = null);
```

#### `AuditContext` (Sealed Record)
```csharp
public sealed record AuditContext(
    string TenantId,
    string Source,
    string? CorrelationId = null,
    string? CausationId = null,
    string? RequestId = null,
    string? IpAddress = null,
    string? UserAgent = null)
{
    public const string SystemTenantId = "system";
}
```

#### `AuditChange` (Sealed Record)
```csharp
public sealed record AuditChange(
    string Field,
    string? OldValue,
    string? NewValue,
    bool IsRedacted = false)
{
    public static AuditChange Redacted(string field);
}
```

#### `AuditOutcome` (Enum)
```csharp
public enum AuditOutcome
{
    Success = 1,
    Failure = 2,
    Denied = 3,
    Cancelled = 4,
    Partial = 5
}
```

### Storage and Provider Interfaces

#### `IAuditStore` (Interface)
Append-only persistence SPI.
```csharp
public interface IAuditStore
{
    ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
```

#### `AuditQuery` & `AuditQueryResult` (Sealed Records)
```csharp
public sealed record AuditQuery
{
    public required string TenantId { get; init; }
    public Guid? AfterRecordId { get; init; } // Keyset seek cursor
    public int PageSize { get; init; } = 50;
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? ActorId { get; init; }
    public string? ActionCode { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public AuditOutcome? Outcome { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed record AuditQueryResult(
    IReadOnlyList<AuditRecord> Records,
    Guid? NextCursorId,
    bool HasMore);
```

#### `IAuditIntegrityVerifier` & `AuditIntegrityVerificationResult`
```csharp
public interface IAuditIntegrityVerifier
{
    ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);
}

public sealed record AuditIntegrityVerificationResult(
    bool IsValid,
    int VerifiedCount,
    Guid? FirstFailedRecordId,
    string? FailureReason);
```

#### `HmacAuditIntegrityService` (Sealed Class)
```csharp
public sealed class HmacAuditIntegrityService
{
    public HmacAuditIntegrityService(IAuditIntegrityProvider keyProvider);
    public string ComputeHash(AuditRecord record, string? previousHash);
    public bool Verify(AuditRecord record);
}
```

#### `IAuditBuilder` (Interface)
```csharp
public interface IAuditBuilder
{
    IServiceCollection Services { get; }
    IAuditBuilder UseStore<TStore>() where TStore : class, IAuditStore;
    IAuditBuilder UseActorProvider<TProvider>() where TProvider : class, IAuditActorProvider;
    IAuditBuilder EnableIntegrityChain();
}
```

---

## 3. Namespace: `EricksonLopez.Auditing.Testing`

```csharp
/// <summary>
/// Thread-safe in-memory audit store for unit testing. Not for production use.
/// </summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    public int Count { get; }
    public IReadOnlyList<AuditRecord> Records { get; }            // Snapshot in insertion order
    public IReadOnlyList<AuditRecord> ForTenant(string tenantId);
    public IReadOnlyList<AuditRecord> ForActor(string actorId);
    public void Clear();
    // IAuditStore members:
    ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fluent builder for constructing AuditRecord test instances.
/// Context fields (TenantId, Source, CorrelationId, etc.) are set via individual With* methods.
/// AuditContext is constructed internally by Build().
/// </summary>
public sealed class AuditRecordBuilder
{
    // Factory methods
    public static AuditRecordBuilder Create();
    public static AuditRecord BuildDefault(
        string tenantId = "tenant-a",
        string actorId = "user-42",
        string resourceType = "Order",
        string resourceId = "order-1",
        AuditOutcome outcome = AuditOutcome.Success,
        string? correlationId = null);

    // Record identity
    public AuditRecordBuilder WithId(Guid id);
    public AuditRecordBuilder WithOccurredAt(DateTimeOffset occurredAt);

    // Actor — two overloads
    public AuditRecordBuilder WithActor(AuditActor actor);
    public AuditRecordBuilder WithActor(AuditActorType type, string id, string? displayName = null);

    // Action — two overloads
    public AuditRecordBuilder WithAction(AuditAction action);
    public AuditRecordBuilder WithAction(string actionCode);

    // Resource — two overloads
    public AuditRecordBuilder WithResource(AuditResource resource);
    public AuditRecordBuilder WithResource(string type, string id, string? aggregateType = null, string? aggregateId = null);

    // Outcome
    public AuditRecordBuilder WithOutcome(AuditOutcome outcome);

    // Context fields (Note: there is no WithContext(AuditContext) method;
    //                 set fields individually — Build() constructs AuditContext internally)
    public AuditRecordBuilder WithTenant(string tenantId);
    public AuditRecordBuilder WithSource(string source);
    public AuditRecordBuilder WithCorrelationId(string? correlationId);
    public AuditRecordBuilder WithCausationId(string? causationId);
    public AuditRecordBuilder WithRequestId(string? requestId);
    public AuditRecordBuilder WithIpAddress(string? ipAddress);
    public AuditRecordBuilder WithUserAgent(string? userAgent);

    // Optional record fields
    public AuditRecordBuilder WithErrorCode(string? errorCode);
    public AuditRecordBuilder WithIntegrityHash(string? hash);
    public AuditRecordBuilder WithPreviousHash(string? previousHash);

    // Changes
    public AuditRecordBuilder AddChange(string field, string? oldValue, string? newValue, bool isRedacted = false);
    public AuditRecordBuilder AddRedactedChange(string field);
    public AuditRecordBuilder WithChanges(IReadOnlyList<AuditChange>? changes);

    // Terminal
    public AuditRecord Build();
}

public sealed class TestAuditIntegrityProvider : IAuditIntegrityProvider
{
    public static readonly byte[] DefaultKey; // 32-byte sequential test key
    public TestAuditIntegrityProvider();                // Uses DefaultKey
    public TestAuditIntegrityProvider(byte[] defaultKey);
    public TestAuditIntegrityProvider SetTenantKey(string tenantId, byte[] key); // Returns self for chaining
    public ReadOnlyMemory<byte> GetCurrentKey(string tenantId);
}
```

---

## 4-12. Storage Adapters & Observability

### PostgreSQL (`EricksonLopez.Auditing.PostgreSql`)
```csharp
public static class PostgreSqlAuditExtensions
{
    public static IAuditBuilder UsePostgreSql(
        this IAuditBuilder builder,
        Action<PostgreSqlAuditStoreOptions> configure);
}
```

### SQL Server (`EricksonLopez.Auditing.SqlServer`)
```csharp
public static class SqlServerAuditExtensions
{
    public static IAuditBuilder UseSqlServer(
        this IAuditBuilder builder,
        Action<SqlServerAuditStoreOptions> configure);
}
```

### SQLite (`EricksonLopez.Auditing.Sqlite`)
```csharp
public static class SqliteAuditExtensions
{
    public static IAuditBuilder UseSqlite(
        this IAuditBuilder builder,
        Action<SqliteAuditStoreOptions> configure);
}
```

### MySQL (`EricksonLopez.Auditing.MySql`)
```csharp
public static class MySqlAuditExtensions
{
    public static IAuditBuilder UseMySql(
        this IAuditBuilder builder,
        Action<MySqlAuditStoreOptions> configure);
}
```

### Oracle (`EricksonLopez.Auditing.Oracle`)
```csharp
public static class OracleAuditExtensions
{
    public static IAuditBuilder UseOracle(
        this IAuditBuilder builder,
        Action<OracleAuditStoreOptions> configure);
}
```

### MongoDB (`EricksonLopez.Auditing.MongoDb`)
```csharp
public static class AuditingMongoDbExtensions
{
    public static IAuditBuilder AddMongoDbAuditStore(
        this IAuditBuilder builder,
        Action<MongoAuditStoreOptions> configure);
}
```

### Dapper Generic (`EricksonLopez.Auditing.Dapper`)
```csharp
public static class DapperAuditExtensions
{
    public static IAuditBuilder UseDapper(
        this IAuditBuilder builder,
        Action<DapperAuditStoreOptions> configure);
}
```

### EF Core (`EricksonLopez.Auditing.EntityFrameworkCore`)

> **Note:** Unlike other adapters, this extension targets `IServiceCollection` directly (not `IAuditBuilder`). Call it separately from `AddAuditing()`.

```csharp
public static class AuditingEfCoreExtensions
{
    // Registers EfCoreAuditStore as IServiceCollection extension (not IAuditBuilder chain)
    public static IServiceCollection AddEntityFrameworkCoreAuditStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext);
}
```

**Usage:**
```csharp
// Register EF Core store (cannot be chained with AddAuditing())
builder.Services.AddAuditing(cfg => { ... });
builder.Services.AddEntityFrameworkCoreAuditStore(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuditDb"));
});
```

### OpenTelemetry (`EricksonLopez.Auditing.OpenTelemetry`)
```csharp
/// <summary>
/// Extension method on AuditRecord. Enriches the current System.Diagnostics.Activity
/// with audit semantic tags using the audit.* tag convention.
/// </summary>
public static class AuditingOpenTelemetryExtensions
{
    public static void EnrichCurrentActivity(this AuditRecord record); // Call as record.EnrichCurrentActivity()
}

public static class AuditActivitySource
{
    public const string ActivitySourceName = "EricksonLopez.Auditing"; // Use with .AddSource(AuditActivitySource.ActivitySourceName)
    public static readonly ActivitySource Source; // Version "1.0.0"

    /// <summary>Semantic OpenTelemetry attribute name constants for audit spans.</summary>
    public static class Tags
    {
        public const string TenantId     = "audit.tenant_id";
        public const string ActionCode   = "audit.action_code";
        public const string ResourceType = "audit.resource_type";
        public const string ResourceId   = "audit.resource_id";
        public const string ActorId      = "audit.actor_id";
        public const string ActorType    = "audit.actor_type";
        public const string Outcome      = "audit.outcome";
        public const string RecordId     = "audit.record_id";
    }
}

public static class AuditMetrics
{
    public const string MeterName = "EricksonLopez.Auditing";

    // OTel Counters:
    public static readonly Counter<long> RecordsAppended;      // "audit.records_appended"
    public static readonly Counter<long> QueriesExecuted;      // "audit.queries_executed"
    public static readonly Counter<long> IntegrityVerifications; // "audit.integrity_verifications"
}
```

**Registration in Program.cs:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AuditActivitySource.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(AuditMetrics.MeterName));
```
