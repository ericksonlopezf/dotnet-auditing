<!-- Copyright © Erickson Lopez. MIT License. -->
# Public API Reference: EricksonLopez.Auditing

Comprehensive Microsoft Learn-style API specification for all public types, methods, options, and extension methods across the 15 core and infrastructure libraries in the `EricksonLopez.Auditing` ecosystem.

---

## Table of Contents

1. [EricksonLopez.Auditing (Core)](#1-package-ericksonlopezauditing--namespace-ericksonlopezauditing)
2. [EricksonLopez.Auditing.Abstractions](#2-package-ericksonlopezauditingabstractions--namespace-ericksonlopezauditing)
3. [EricksonLopez.Auditing.AzureKeyVault](#3-package-ericksonlopezauditingazurekeyvault)
4. [EricksonLopez.Auditing.Outbox](#4-package-ericksonlopezauditingoutbox)
5. [EricksonLopez.Auditing.Dapper](#5-package-ericksonlopezauditingdapper)
6. [EricksonLopez.Auditing.PostgreSql](#6-package-ericksonlopezauditingpostgresql)
7. [EricksonLopez.Auditing.SqlServer](#7-package-ericksonlopezauditingsqlserver)
8. [EricksonLopez.Auditing.Sqlite](#8-package-ericksonlopezauditingsqlite)
9. [EricksonLopez.Auditing.MySql](#9-package-ericksonlopezauditingmysql)
10. [EricksonLopez.Auditing.Oracle](#10-package-ericksonlopezauditingoracle)
11. [EricksonLopez.Auditing.MongoDb](#11-package-ericksonlopezauditingmongodb)
12. [EricksonLopez.Auditing.EntityFrameworkCore](#12-package-ericksonlopezauditingentityframeworkcore)
13. [EricksonLopez.Auditing.OpenTelemetry](#13-package-ericksonlopezauditingopentelemetry)
14. [EricksonLopez.Auditing.Testing](#14-package-ericksonlopezauditingtesting)
15. [EricksonLopez.Auditing.Analyzers](#15-package-ericksonlopezauditinganalyzers)

---

## 1. Package: `EricksonLopez.Auditing` — Namespace: `EricksonLopez.Auditing`

### `AuditId` (Static Class)
Provides monotonic, timestamp-ordered UUID generation according to RFC 9562 (UUIDv7).

```csharp
public static class AuditId
{
    public static Guid NewId();
}
```
* **Return**: Monotonically ordered `Guid` with 48-bit millisecond timestamp in high bits.
* **Exceptions**: None.
* **Performance**: Zero allocations on .NET 9+ (`Guid.CreateVersion7()`), sub-microsecond stackalloc implementation on .NET 8.
* **Best Practices**: Use as the default primary key generator for all `AuditRecord.Id` instances to prevent B-Tree fragmentation.

---

### `AuditScope` (Sealed Class, `IDisposable`)
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
* **Important:** `AuditScope` implements only `IDisposable`, **not** `IAsyncDisposable`. Use `using var scope = AuditScope.Begin(...)`. The `await using` pattern will produce a compile-time error.

---

### `AuditConfiguration` (Sealed Class)
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
    public int MaxStringLength { get; set; } = 4000;
}
```

---

### `AuditFailureBehavior` (Enum)
Specifies policy when audit store persistence fails.

```csharp
public enum AuditFailureBehavior
{
    FailClosed = 1, // Store failures propagate and abort caller operations (default)
    FailOpen = 2,   // Store failures are logged/swallowed, allowing business execution to proceed
    Deferred = 3    // Events are buffered locally for deferred background retries
}
```

---

### `AuditFieldSensitivity` (Enum)
Specifies field-level handling in change tracking records.

```csharp
public enum AuditFieldSensitivity
{
    Include = 0, // Recorded as-is
    Exclude = 1, // Excluded from the record entirely
    Redact = 2,  // Recorded with value replaced by redaction marker
    Hash = 3     // Replaced by one-way SHA-256 digest
}
```

---

### `AuditSensitivityPipeline` (Sealed Class, `IAuditSensitivityPipeline`)
Sanitizes change tracking records against denylists, redaction markers, and maximum string lengths.

```csharp
public sealed class AuditSensitivityPipeline : IAuditSensitivityPipeline
{
    public AuditSensitivityPipeline(AuditConfiguration config, IAuditCryptoKeyProvider? keyProvider = null);
    public ValueTask<AuditRecord> SanitizeAsync(AuditRecord record, CancellationToken cancellationToken = default);
    public ValueTask<IReadOnlyList<AuditChange>?> ApplyAsync(IReadOnlyList<AuditChange>? changes, string tenantId, CancellationToken cancellationToken = default);
    public static string HashValue(string value, string? salt = null);
}
```

---

### `AuditRecordBuilder` (Sealed Class)
Fluent builder for constructing immutable `AuditRecord` instances.

```csharp
public sealed class AuditRecordBuilder
{
    public static AuditRecordBuilder Create();
    public static AuditRecord BuildDefault(string tenantId = "tenant-a", string actorId = "user-42", string resourceType = "Order", string resourceId = "order-1", AuditOutcome outcome = AuditOutcome.Success, string? correlationId = null);
    public AuditRecordBuilder WithId(Guid id);
    public AuditRecordBuilder WithOccurredAt(DateTimeOffset occurredAt);
    public AuditRecordBuilder WithActor(AuditActor actor);
    public AuditRecordBuilder WithActor(AuditActorType type, string id, string? displayName = null);
    public AuditRecordBuilder WithAction(AuditAction action);
    public AuditRecordBuilder WithAction(string code);
    public AuditRecordBuilder WithResource(AuditResource resource);
    public AuditRecordBuilder WithResource(string type, string id, string? aggregateType = null, string? aggregateId = null);
    public AuditRecordBuilder WithOutcome(AuditOutcome outcome);
    public AuditRecordBuilder WithTenant(string tenantId);
    public AuditRecordBuilder WithSource(string source);
    public AuditRecordBuilder WithCorrelationId(string? correlationId);
    public AuditRecordBuilder WithCausationId(string? causationId);
    public AuditRecordBuilder WithRequestId(string? requestId);
    public AuditRecordBuilder WithIpAddress(string? ipAddress);
    public AuditRecordBuilder WithUserAgent(string? userAgent);
    public AuditRecordBuilder WithErrorCode(string? errorCode);
    public AuditRecordBuilder WithIntegrityHash(string? hash, string? previousHash = null);
    public AuditRecordBuilder WithPreviousHash(string? previousHash);
    public AuditRecordBuilder AddChange(string field, string? oldValue, string? newValue, bool isRedacted = false);
    public AuditRecordBuilder AddRedactedChange(string field);
    public AuditRecordBuilder WithChanges(IEnumerable<AuditChange>? changes);
    public AuditRecord Build();
}
```

---

### `AuditLogger<TCategoryName>` (Sealed Class, `IAuditLogger<TCategoryName>`)
Facade for creating and persisting audit records with automatically resolved ambient context and actor.

```csharp
public sealed class AuditLogger<TCategoryName> : IAuditLogger<TCategoryName>
{
    public AuditLogger(IAuditStore store, IAuditActorProvider actorProvider, IAuditContextProvider? contextProvider = null);
    public ValueTask LogAsync(AuditAction action, AuditResource resource, AuditOutcome outcome, CancellationToken cancellationToken = default);
}
```

---

### Pipeline Decorators & Buffering

#### `BufferedAuditStoreDecorator` (Sealed Class, `IAuditStore`, `IAsyncDisposable`, `IDisposable`)
Decorates an `IAuditStore` with high-throughput channel buffering and background batch drain.

```csharp
public sealed class BufferedAuditStoreDecorator : IAuditStore, IAsyncDisposable, IDisposable
{
    public BufferedAuditStoreDecorator(IAuditStore innerStore, BufferedAuditStoreOptions? options = null, ILogger<BufferedAuditStoreDecorator>? logger = null);
    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
    public void Dispose();
}
```

#### `BufferedAuditStoreOptions` (Sealed Class)
```csharp
public sealed class BufferedAuditStoreOptions
{
    public int Capacity { get; set; } = 10_000;                          // Default: 10,000 records
    public int BatchSize { get; set; } = 100;                            // Default: 100 records per flush
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(500); // Default: 500 ms
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;
    public int WorkerCount { get; set; } = 1; // Default: 1 background drain worker
}
```

#### `ResilientAuditStoreDecorator` (Sealed Class, `IAuditStore`)
Enforces `AuditFailureBehavior` (FailClosed/FailOpen) and protects `CriticalActionCodes`.

```csharp
public sealed class ResilientAuditStoreDecorator : IAuditStore
{
    public ResilientAuditStoreDecorator(IAuditStore innerStore, AuditConfiguration configuration, ILogger<ResilientAuditStoreDecorator>? logger = null);
    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
```

#### `IntegrityAuditStoreDecorator` (Sealed Class, `IAuditStore`)
Automatically calculates and chains HMAC-SHA256 digests onto records before forwarding to the underlying store.

```csharp
public sealed class IntegrityAuditStoreDecorator : IAuditStore
{
    public IntegrityAuditStoreDecorator(IAuditStore innerStore, HmacAuditIntegrityService integrityService);
    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
```

#### `AuditingServiceCollectionExtensions` (Static Class)
```csharp
public static class AuditingServiceCollectionExtensions
{
    /// <summary>Adds core auditing services, configuration, pipeline, and default providers.</summary>
    public static IAuditBuilder AddAuditing(this IServiceCollection services, Action<AuditConfiguration>? configure = null);

    /// <summary>Decorates the registered IAuditStore with a high-throughput asynchronous batching buffer.</summary>
    public static IAuditBuilder EnableBuffering(this IAuditBuilder builder, Action<BufferedAuditStoreOptions>? configure = null);

    /// <summary>Applies any pending decorators (HMAC integrity, buffering) to the registered audit store.</summary>
    public static IAuditBuilder ApplyDecorators(this IAuditBuilder builder);
}
```

---

## 2. Package: `EricksonLopez.Auditing.Abstractions` — Namespace: `EricksonLopez.Auditing`

### Domain Contracts

#### `AuditRecord` (Sealed Record)
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

#### `TenantId` (Readonly Record Struct)
Strongly typed value object representing a tenant identifier.
```csharp
public readonly record struct TenantId
{
    public string Value { get; }
    public TenantId(string value);  // throws ArgumentException if null or whitespace
    public static implicit operator TenantId(string value);
    public static implicit operator string(TenantId tenantId);
    public override string ToString();
}
```
* **Note:** There is no `TenantId.Default` field. The zero-value of this struct has a `null` `Value` and is invalid by itself. Use `new TenantId("...")` or the implicit `string` conversion.

#### `AuditCursorToken` (Static Class)
Encodes and decodes keyset cursor pagination tokens.
```csharp
public static class AuditCursorToken
{
    public static string Create(DateTimeOffset occurredAt, Guid id);
    public static bool TryParse(string? token, out DateTimeOffset occurredAt, out Guid id);
}
```

#### `AuditActor` (Sealed Record) & `AuditActorType` (Enum)
```csharp
public enum AuditActorType : byte
{
    User = 1,
    SystemProcess = 2,
    Service = 3,
    ScheduledJob = 4,
    Integration = 5,
    Anonymous = 6
}

public sealed record AuditActor(AuditActorType Type, string Id, string? DisplayName = null)
{
    public static readonly AuditActor Anonymous;
    public static readonly AuditActor System;
}
```

#### `AuditAction` (Readonly Record Struct)
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
public sealed record AuditResource(string Type, string Id, string? AggregateType = null, string? AggregateId = null);
```

#### `AuditContext` (Sealed Record)
```csharp
public sealed record AuditContext(
    TenantId TenantId,
    string Source,
    string? CorrelationId = null,
    string? CausationId = null,
    string? RequestId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? IdempotencyKey = null)
{
    public static readonly TenantId SystemTenantId;  // = new TenantId("system")
    public static string? AnonymizeIp(string? ip);
}
```
* **`SystemTenantId`**: A `static readonly TenantId` (not `const string`) initialized to `new TenantId("system")`. It cannot be used in attribute arguments, switch case labels, or other compile-time-constant contexts.

#### `AuditChange` (Sealed Record)
```csharp
public sealed record AuditChange(string Field, string? OldValue, string? NewValue, bool IsRedacted = false)
{
    public static AuditChange Redacted(string field);
}
```

#### `AuditOutcome` (Enum)
```csharp
public enum AuditOutcome : byte
{
    Success = 1,
    Failure = 2,
    Denied = 3,
    Cancelled = 4,
    Partial = 5
}
```

#### `AuditQuery` & `AuditQueryResult` (Sealed Records)
```csharp
public sealed record AuditQuery
{
    public required TenantId TenantId { get; init; }
    public string? ActorId { get; init; }
    public string? ActionCode { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? CorrelationId { get; init; }
    public AuditOutcome? Outcome { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? ContinuationToken { get; init; }  // Keyset pagination token from previous AuditQueryResult.NextPageToken
    public int PageSize { get; init; } = 50;
}

public sealed record AuditQueryResult(
    IReadOnlyList<AuditRecord> Records,
    string? NextPageToken,   // Opaque base64 cursor token; null when no more pages exist
    bool HasMore);
```
* **Pagination**: Use `ContinuationToken = result.NextPageToken` in the next query to advance the cursor. Check `HasMore && NextPageToken is not null` to detect remaining pages. See `AuditCursorToken` for token encoding details.

### SPI Interfaces

* `IAuditStore`: Contract for appending and querying audit records.
* `IAuditIntegrityVerifier`: Cryptographic chain verification contract (`VerifyChainAsync`).
* `IAuditIntegrityProvider`: Tenant HMAC cryptographic key resolution contract (`GetCurrentKey`).
* `IAuditCryptoKeyProvider`: GDPR Art. 17 Crypto-shredding key manager (`GetEncryptionKeyAsync`, `ShredKeyAsync`).
* `IAuditHashAlgorithm`: Low-level cryptographic digest SPI (`ComputeHash`, `HashLengthInBytes`, `AlgorithmId`).
* `HmacSha256AuditHashAlgorithm`: Default implementation of `IAuditHashAlgorithm`.
* `IAuditActorProvider`: Ambient actor resolution contract (`GetCurrentActor`).
* `SystemAuditActorProvider`: Predefined singleton returning `AuditActor.System`.
* `IAuditContextProvider`: Ambient execution context resolution contract (`GetCurrentContext`).
* `IAuditTimeProvider` & `AmbientAuditTimeProvider`: Deterministic and scoped UTC time resolution.
* `IAuditLogger` & `IAuditLogger<TCategoryName>`: Facade for logging audit records.
* `IAuditBuilder`: Fluent configuration builder (`Services`, `UseActorProvider<T>`, `EnableIntegrityChain`, `UseStore<T>`).

---

## 3. Package: `EricksonLopez.Auditing.AzureKeyVault`

### `AzureKeyVaultIntegrityProvider` (Sealed Class, `IAuditIntegrityProvider`)
Retrieves and caches cryptographic tenant HMAC keys securely from Azure Key Vault secrets.

```csharp
public sealed class AzureKeyVaultIntegrityProvider : IAuditIntegrityProvider
{
    public AzureKeyVaultIntegrityProvider(Uri keyVaultUri);
    public ReadOnlyMemory<byte> GetCurrentKey(TenantId tenantId);
}
```
* **Secret Convention**: Expects secrets named `audit-key-{tenantId}` containing a Base64-encoded 256-bit key.
* **Caching**: Keys are cached in a thread-safe `ConcurrentDictionary` to prevent outbound latency on write paths.

---

## 4. Package: `EricksonLopez.Auditing.Outbox`

### `OutboxAuditStore` (Sealed Class, `IAuditStore`)
An `IAuditStore` decorator that stages audit records into a transactional outbox alongside business transactions.

```csharp
public sealed class OutboxAuditStore : IAuditStore
{
    public OutboxAuditStore(IOutboxMessageService outbox);
    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
```

### `IOutboxMessageService` (Interface)
Contract for appending outbox messages in the current unit of work.

```csharp
public interface IOutboxMessageService
{
    ValueTask AppendMessageAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}
```

---

## 5. Package: `EricksonLopez.Auditing.Dapper`

```csharp
public static class DapperAuditExtensions
{
    public static IAuditBuilder UseDapper(this IAuditBuilder builder, Action<DapperAuditStoreOptions> configure);
}

public sealed class DapperAuditStore : IAuditStore
{
    public DapperAuditStore(DapperAuditStoreOptions options);
    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
    public ValueTask<AuditRecord?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);
}

public sealed class DapperAuditStoreOptions
{
    public Func<IDbConnection> ConnectionFactory { get; set; }
    public string Table { get; set; } = "audit_records";
    public string Schema { get; set; } = "dbo";
}
```

---

## 6. Package: `EricksonLopez.Auditing.PostgreSql`

```csharp
public static class PostgreSqlAuditExtensions
{
    public static IAuditBuilder UsePostgreSql(this IAuditBuilder builder, Action<PostgreSqlAuditStoreOptions> configure);
}

public sealed class PostgreSqlAuditStore : IAuditStore { ... }
public sealed class PostgreSqlAuditIntegrityVerifier : IAuditIntegrityVerifier { ... }
public sealed class PostgreSqlAuditStoreOptions { ... }
```

---

## 7. Package: `EricksonLopez.Auditing.SqlServer`

```csharp
public static class SqlServerAuditExtensions
{
    public static IAuditBuilder UseSqlServer(this IAuditBuilder builder, Action<SqlServerAuditStoreOptions> configure);
}

public sealed class SqlServerAuditStore : IAuditStore { ... }
public sealed class SqlServerAuditIntegrityVerifier : IAuditIntegrityVerifier { ... }
public sealed class SqlServerAuditStoreOptions { ... }
```

---

## 8. Package: `EricksonLopez.Auditing.Sqlite`

```csharp
public static class SqliteAuditExtensions
{
    public static IAuditBuilder UseSqlite(this IAuditBuilder builder, Action<SqliteAuditStoreOptions> configure);
}

public sealed class SqliteAuditStore : IAuditStore { ... }
public sealed class SqliteAuditIntegrityVerifier : IAuditIntegrityVerifier { ... }
public sealed class SqliteAuditStoreOptions { ... }
```

---

## 9. Package: `EricksonLopez.Auditing.MySql`

```csharp
public static class MySqlAuditExtensions
{
    public static IAuditBuilder UseMySql(this IAuditBuilder builder, Action<MySqlAuditStoreOptions> configure);
}

public sealed class MySqlAuditStore : IAuditStore { ... }
public sealed class MySqlAuditIntegrityVerifier : IAuditIntegrityVerifier { ... }
public sealed class MySqlAuditStoreOptions { ... }
```

---

## 10. Package: `EricksonLopez.Auditing.Oracle`

```csharp
public static class OracleAuditExtensions
{
    public static IAuditBuilder UseOracle(this IAuditBuilder builder, Action<OracleAuditStoreOptions> configure);
}

public sealed class OracleAuditStore : IAuditStore { ... }
public sealed class OracleAuditIntegrityVerifier : IAuditIntegrityVerifier { ... }
public sealed class OracleAuditStoreOptions { ... }
```

---

## 11. Package: `EricksonLopez.Auditing.MongoDb`

```csharp
public static class AuditingMongoDbExtensions
{
    public static IServiceCollection AddMongoDbAuditStore(this IServiceCollection services, Func<IServiceProvider, IMongoDatabase> databaseFactory, Action<MongoAuditStoreOptions>? configure = null);
}

public sealed class MongoAuditStore : IAuditStore { ... }
public sealed class MongoAuditStoreOptions { ... }
public sealed class MongoAuditRecordDocument { ... }
public sealed class MongoAuditChangeDocument { ... }
```

---

## 12. Package: `EricksonLopez.Auditing.EntityFrameworkCore`

```csharp
public static class AuditingEfCoreExtensions
{
    public static IServiceCollection AddEntityFrameworkCoreAuditStore(this IServiceCollection services, Action<DbContextOptionsBuilder> optionsAction);
}

public static class AuditDbContextModelBuilderExtensions
{
    public static ModelBuilder ApplyAuditRecordConfiguration(this ModelBuilder modelBuilder, string tableName = "audit_records", string? schema = null);
}

public class AuditDbContext : DbContext { ... }
public sealed class EfCoreAuditStore : IAuditStore { ... }
public sealed class AuditRecordEntity { ... }
```

---

## 13. Package: `EricksonLopez.Auditing.OpenTelemetry`

```csharp
public static class AuditingOpenTelemetryExtensions
{
    public static void EnrichCurrentActivity(this AuditRecord record);
    public static IAuditBuilder AddOpenTelemetryInstrumentation(this IAuditBuilder builder);
}

public static class AuditActivitySource
{
    public const string ActivitySourceName = "EricksonLopez.Auditing";
    public static readonly ActivitySource Source;
    public static class Tags { ... }
}

public static class AuditMetrics
{
    public const string MeterName = "EricksonLopez.Auditing";
    public static readonly Counter<long> RecordsAppended;
    public static readonly Counter<long> RecordsFailed;
    public static readonly Counter<long> QueriesExecuted;
    public static readonly Counter<long> IntegrityVerifications;
    public static readonly Histogram<double> AppendDuration;
    public static readonly Histogram<double> QueryDuration;
}

public sealed class OpenTelemetryAuditStoreDecorator : IAuditStore { ... }
public sealed class OpenTelemetryAuditIntegrityVerifierDecorator : IAuditIntegrityVerifier { ... }
```

---

## 14. Package: `EricksonLopez.Auditing.Testing`

```csharp
public sealed class InMemoryAuditStore : IAuditStore
{
    public int Count { get; }
    public IReadOnlyList<AuditRecord> Records { get; }
    public IReadOnlyList<AuditRecord> ForTenant(string tenantId);
    public IReadOnlyList<AuditRecord> ForActor(string actorId);
    public void Clear();
}

public sealed class TestAuditIntegrityProvider : IAuditIntegrityProvider
{
    public static readonly byte[] DefaultKey;
    public void SetTenantKey(string tenantId, byte[] key);
    public ReadOnlyMemory<byte> GetCurrentKey(TenantId tenantId);
}
```

---

## 15. Package: `EricksonLopez.Auditing.Analyzers`

Diagnostic analyzers and compile-time code fixes targeting `netstandard2.0` to enforce architectural invariants and prevent runtime leaks:

### Diagnostic Rules

| Rule ID | Severity | Category | Description |
| :--- | :---: | :--- | :--- |
| **`AUD001`** | `Warning` | Usage | Enforces that `AuditScope.Begin(...)` is captured within a `using` statement or `using` declaration to prevent ambient `AsyncLocal<T>` context leakage into surrounding asynchronous execution flows. |

```csharp
namespace EricksonLopez.Auditing.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AuditScopeUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "AUD001";
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
    public override void Initialize(AnalysisContext context);
}
```

