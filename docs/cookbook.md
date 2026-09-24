<!-- Copyright © Erickson Lopez. MIT License. -->
# Official Integration Cookbook & Recipes

Copy-paste integration recipes for common real-world scenarios using public APIs of `EricksonLopez.Auditing`.

---

## Table of Recipes

1. [Recipe 1: Automatic User Identity from HttpContext](#recipe-1-automatic-user-identity-from-httpcontext)
2. [Recipe 2: Sensitive Data Redaction & One-Way Hashing (GDPR / PCI-DSS)](#recipe-2-sensitive-data-redaction--one-way-hashing-gdpr--pci-dss)
3. [Recipe 3: Ambient Scopes and Nested Workflow Correlation](#recipe-3-ambient-scopes-and-nested-workflow-correlation)
4. [Recipe 4: Batch Persistence with Multi-Tenant Homogeneity](#recipe-4-batch-persistence-with-multi-tenant-homogeneity)
5. [Recipe 5: Verifying Cryptographic HMAC-SHA256 Integrity Chains](#recipe-5-verifying-cryptographic-hmac-sha256-integrity-chains)
6. [Recipe 6: OpenTelemetry Distributed Tracing & Custom Metrics](#recipe-6-opentelemetry-distributed-tracing--custom-metrics)
7. [Recipe 7: Unit Testing with InMemoryAuditStore & Fluent Builders](#recipe-7-unit-testing-with-inmemoryauditstore--fluent-builders)
8. [Recipe 8: Transactional Outbox Pattern for Zero Dual-Write Risk](#recipe-8-transactional-outbox-pattern-for-zero-dual-write-risk)
9. [Recipe 9: High-Throughput Non-Blocking Channel Buffering](#recipe-9-high-throughput-non-blocking-channel-buffering)
10. [Recipe 10: Resilient FailOpen vs Critical Action Protection](#recipe-10-resilient-failopen-vs-critical-action-protection)
11. [Recipe 11: Enterprise Cloud Key Management with Azure Key Vault](#recipe-11-enterprise-cloud-key-management-with-azure-key-vault)
12. [Recipe 12: GDPR Article 17 Crypto-Shredding ("Right to be Forgotten")](#recipe-12-gdpr-article-17-crypto-shredding-right-to-be-forgotten)

---

## Recipe 1: Automatic User Identity from HttpContext

### Problem
Extract authenticated user claims in ASP.NET Core without manually injecting user parameters into every domain service.

### Solution
Implement `IAuditActorProvider` and register it with `UseActorProvider<T>()`.

```csharp
using System.Security.Claims;
using EricksonLopez.Auditing;
using Microsoft.AspNetCore.Http;

public sealed class HttpContextAuditActorProvider : IAuditActorProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditActorProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuditActor GetCurrentActor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null || !user.Identity?.IsAuthenticated == true)
        {
            return AuditActor.Anonymous;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? user.FindFirst("sub")?.Value 
                     ?? "unknown-user";

        var name = user.FindFirst(ClaimTypes.Name)?.Value 
                   ?? user.FindFirst("email")?.Value;

        return new AuditActor(AuditActorType.User, userId, name);
    }
}

// DI Registration:
services.AddHttpContextAccessor();
services.AddAuditing()
    .UseActorProvider<HttpContextAuditActorProvider>()
    .UsePostgreSql(opts => ...);
```

---

## Recipe 2: Sensitive Data Redaction & One-Way Hashing (GDPR / PCI-DSS)

### Problem
Ensure credit card numbers, passwords, or PII are not persisted in plain text while retaining audit evidence that the field was modified.

### Solution
Use `AuditChange.Redacted()` or static `AuditSensitivityPipeline.HashValue()`.

```csharp
var changes = new List<AuditChange>
{
    new("Email", "old@domain.com", "new@domain.com"),
    
    // Explicit redaction suppresses both OldValue and NewValue
    AuditChange.Redacted("TaxIdentificationNumber"),
    
    // One-way SHA-256 hash permits equality checks without revealing plain text
    new("PasswordVerificationHash", null, AuditSensitivityPipeline.HashValue("UserSecureSecret!99"))
};

var record = new AuditRecord
{
    Id = AuditId.NewId(),
    OccurredAt = DateTimeOffset.UtcNow,
    Actor = new AuditActor(AuditActorType.User, "usr-admin"),
    Action = AuditAction.Update,
    Resource = new AuditResource("UserProfile", "usr-profile-10"),
    Outcome = AuditOutcome.Success,
    Context = new AuditContext("tenant-eu", "AdminPortal"),
    Changes = changes
};
```

---

## Recipe 3: Ambient Scopes and Nested Workflow Correlation

### Problem
Propagate correlation IDs and business transaction metadata across asynchronous helper methods without modifying method signatures.

### Solution
Use `AuditScope.Begin()` and `scope.WithMetadata()`. `AuditScope` utilizes `AsyncLocal<T>` and restores parent state upon disposal.

```csharp
using (var parentScope = AuditScope.Begin())
{
    parentScope.WithMetadata("Operation", "InvoiceProcessing")
               .WithMetadata("BatchId", "batch-2026-08");

    await ProcessInvoiceAsync("inv-001");

    // Nested child scope with isolated modifications
    using (var childScope = AuditScope.Begin())
    {
        childScope.WithMetadata("Step", "PaymentGatewayCall");
        await ExecutePaymentCallAsync();
    } // childScope disposed, parentScope metadata preserved exactly

    await FinalizeInvoiceAsync("inv-001");
}
```

---

## Recipe 4: Batch Persistence with Multi-Tenant Homogeneity

### Problem
Insert high-volume audit records in batch workers efficiently while respecting database-level Row-Level Security.

### Solution
Group records by `TenantId` before invoking `AppendBatchAsync()`.

```csharp
public async Task ProcessAuditQueueAsync(IReadOnlyList<AuditRecord> records, IAuditStore auditStore, CancellationToken ct)
{
    // Storage engines require single-tenant homogeneity per batch
    var tenantBatches = records.GroupBy(r => r.Context.TenantId);

    foreach (var batch in tenantBatches)
    {
        await auditStore.AppendBatchAsync(batch.ToList(), ct);
    }
}
```

---

## Recipe 5: Verifying Cryptographic HMAC-SHA256 Integrity Chains

### Problem
Audit database records periodically to verify that no rows were inserted, deleted, or altered out-of-band by database administrators.

### Solution
Inject `IAuditIntegrityVerifier` and execute `VerifyChainAsync()`.

```csharp
public async Task RunDailyAuditIntegrityCheckAsync(
    IAuditIntegrityVerifier verifier,
    string tenantId,
    ILogger logger,
    CancellationToken ct)
{
    var from = DateTimeOffset.UtcNow.AddDays(-1);
    var to = DateTimeOffset.UtcNow;

    AuditIntegrityVerificationResult result = await verifier.VerifyChainAsync(tenantId, from, to, ct);

    if (result.IsValid)
    {
        logger.LogInformation("Integrity verified for {TenantId}. {Count} records checked.", tenantId, result.VerifiedCount);
    }
    else
    {
        logger.LogCritical("TAMPER DETECTED in {TenantId}! Failed record: {RecordId}. Reason: {Reason}",
            tenantId, result.FirstFailedRecordId, result.FailureReason);
        // Trigger security alert / pager duty
    }
}
```

---

## Recipe 6: OpenTelemetry Distributed Tracing & Custom Metrics

### Problem
Correlate audit records with W3C distributed trace activities and monitor audit throughput in Prometheus/Grafana.

```csharp
// 1. In Program.cs:
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AuditActivitySource.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(AuditMetrics.MeterName));

// 2. In your service:
public async Task CompleteOrderAsync(AuditRecord record, IAuditStore store, CancellationToken ct)
{
    await store.AppendAsync(record, ct);

    // Enriches current System.Diagnostics.Activity with audit.* semantic tags
    record.EnrichCurrentActivity();
}
```

---

## Recipe 7: Unit Testing with InMemoryAuditStore & Fluent Builders

### Problem
Write fast, isolated unit tests for business services that emit audit records without spinning up Docker or external databases.

```csharp
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public async Task CancelOrder_EmitsCorrectAuditRecord()
    {
        // Arrange
        var store = new InMemoryAuditStore();
        var sut = new OrderService(store);

        // Act
        await sut.CancelOrderAsync("ord-100", "tenant-test", CancellationToken.None);

        // Assert
        store.Count.Should().Be(1);
        var record = store.ForTenant("tenant-test").Single();
        record.Action.Should().Be(AuditAction.Cancel);
        record.Resource.Id.Should().Be("ord-100");
        record.Outcome.Should().Be(AuditOutcome.Success);
    }
}
```

---

## Recipe 8: Transactional Outbox Pattern for Zero Dual-Write Risk

### Problem
In distributed or high-throughput microservices, writing to both the business database and an audit database synchronously creates dual-write inconsistency if the audit database is slow or temporarily unreachable.

### Solution
Use `OutboxAuditStore` from `EricksonLopez.Auditing.Outbox`. This decorator stages the serialized audit record in your local database transactional outbox table within the same database transaction as the business operation.

```csharp
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Outbox;

// 1. Implement your application's outbox repository
public sealed class EfCoreOutboxMessageService : IOutboxMessageService
{
    private readonly AppDbContext _dbContext;
    public EfCoreOutboxMessageService(AppDbContext dbContext) => _dbContext = dbContext;

    public ValueTask AppendMessageAsync(string eventType, string payload, CancellationToken ct = default)
    {
        _dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        });
        return ValueTask.CompletedTask;
    }
}

// 2. Register OutboxAuditStore as your IAuditStore in DI:
services.AddScoped<IOutboxMessageService, EfCoreOutboxMessageService>();
services.AddScoped<IAuditStore, OutboxAuditStore>();
```

### Best Practices
* A separate background worker reads outbox messages and forwards them in bulk to your centralized audit cluster.
* Uses native AOT-safe source-generated JSON context.

---

## Recipe 9: High-Throughput Non-Blocking Channel Buffering

### Problem
High-volume APIs (e.g. IoT ingestion, e-commerce checkout) experience latency spikes if database network hops are incurred synchronously on the HTTP request thread.

### Solution
Use `EnableBuffering()` on `IAuditBuilder` or configure `BufferedAuditStoreDecorator`. Audit records are pushed into a bounded `System.Threading.Channels.Channel<T>` and drained asynchronously by a background worker task.

```csharp
services.AddAuditing()
    .UsePostgreSql(opts => ...)
    .EnableBuffering(options =>
    {
        options.Capacity = 5000;                       // Bounded channel queue limit
        options.BatchSize = 100;                        // Drain up to 100 records per bulk write
        options.FlushInterval = TimeSpan.FromSeconds(2); // Flush partial batches after 2 seconds
        options.FullMode = BoundedChannelFullMode.Wait; // Apply backpressure if saturated
    });
```

### Best Practices
* When the application terminates gracefully, the decorator drains any remaining items before exiting (`IAsyncDisposable`).
* In the event of catastrophic inner store exhaustion, failed batches are persisted to a Dead Letter Queue (DLQ) file on disk.

---

## Recipe 10: Resilient FailOpen vs Critical Action Protection

### Problem
You want non-critical user activities (e.g. searching a catalog or viewing a page) to never fail the user even if the database is under maintenance, but strict security events (e.g. `ProcessPayroll`, `GrantPermission`) must unconditionally halt execution if audit persistence fails.

### Solution
Use `ResilientAuditStoreDecorator` and configure `AuditConfiguration.DefaultFailureBehavior` with `CriticalActionCodes`.

```csharp
services.AddAuditing(cfg =>
{
    // Non-critical operations swallow errors and log warnings
    cfg.DefaultFailureBehavior = AuditFailureBehavior.FailOpen;

    // Security-critical operations will always rethrow and abort business flows
    cfg.CriticalActionCodes.Add("ProcessPayroll");
    cfg.CriticalActionCodes.Add("GrantPermission");
    cfg.CriticalActionCodes.Add("DeleteUser");
})
.UseSqlServer(opts => ...);
```

---

## Recipe 11: Enterprise Cloud Key Management with Azure Key Vault

### Problem
Storing static HMAC secret keys in configuration files violates enterprise compliance and key rotation policies.

### Solution
Use `AzureKeyVaultIntegrityProvider` from `EricksonLopez.Auditing.AzureKeyVault`.

```csharp
using Azure.Identity;
using EricksonLopez.Auditing.AzureKeyVault;

var keyVaultUri = new Uri("https://my-company-vault.vault.azure.net/");
services.AddSingleton<IAuditIntegrityProvider>(new AzureKeyVaultIntegrityProvider(keyVaultUri));

services.AddAuditing()
    .EnableIntegrityChain()
    .UseSqlServer(opts => ...);
```

### Best Practices
* Store keys as Base64-encoded 256-bit strings in Key Vault secrets using the naming pattern `audit-key-{tenantId}`.
* Keys are cached in-memory in a thread-safe `ConcurrentDictionary` to eliminate outbound cloud latency on hot write paths.

---

## Recipe 12: GDPR Article 17 Crypto-Shredding ("Right to be Forgotten")

### Problem
Under GDPR Article 17, data subjects have the right to erasure. However, deleting records from an append-only, HMAC-chained cryptographic audit trail would break the hash chain and destroy historical non-repudiation.

### Solution
Implement the **Crypto-Shredding** pattern using `IAuditCryptoKeyProvider`. PII fields are encrypted at rest with a dedicated per-subject key. When an erasure request is executed, the subject's encryption key is destroyed (`ShredKeyAsync`), rendering the encrypted audit fields permanently irrecoverable while leaving the cryptographic block chain intact.

```csharp
using EricksonLopez.Auditing;

public sealed class KmsCryptoKeyProvider : IAuditCryptoKeyProvider
{
    private readonly IKmsClient _kmsClient;
    public KmsCryptoKeyProvider(IKmsClient kmsClient) => _kmsClient = kmsClient;

    public async ValueTask<byte[]> GetEncryptionKeyAsync(string subjectId, CancellationToken ct = default)
    {
        return await _kmsClient.GetOrGenerateDataKeyAsync($"gdpr-{subjectId}", ct);
    }

    public async ValueTask ShredKeyAsync(string subjectId, CancellationToken ct = default)
    {
        // Permanently destroy the key from HSM / KMS
        await _kmsClient.DestroyKeyAsync($"gdpr-{subjectId}", ct);
    }
}

// In DI:
services.AddSingleton<IAuditCryptoKeyProvider, KmsCryptoKeyProvider>();
```

