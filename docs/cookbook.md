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
