# Quick Start Guide: EricksonLopez.Auditing

Get up and running with **`EricksonLopez.Auditing`** in under 5 minutes.

---

## 1. Package Installation

Install the core auditing package along with your chosen database adapter:

```bash
# Core engine & abstractions (Always required)
dotnet add package EricksonLopez.Auditing

# Choose your database adapter:
dotnet add package EricksonLopez.Auditing.PostgreSql      # PostgreSQL with native RLS
dotnet add package EricksonLopez.Auditing.SqlServer       # SQL Server / Azure SQL
dotnet add package EricksonLopez.Auditing.Sqlite          # SQLite for local / edge / test
dotnet add package EricksonLopez.Auditing.MySql           # MySQL / MariaDB
dotnet add package EricksonLopez.Auditing.Oracle          # Oracle Database 19c/21c/23ai
dotnet add package EricksonLopez.Auditing.MongoDb         # MongoDB
dotnet add package EricksonLopez.Auditing.EntityFrameworkCore # EF Core
dotnet add package EricksonLopez.Auditing.Dapper          # Generic ANSI SQL
dotnet add package EricksonLopez.Auditing.Testing         # In-memory test doubles
dotnet add package EricksonLopez.Auditing.OpenTelemetry   # Telemetry spans & metrics
```

---

## 2. Service Registration (`Program.cs`)

### Option A: PostgreSQL (Production Standard with RLS)
```csharp
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.PostgreSql;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuditing(cfg =>
{
    cfg.DefaultFailureBehavior = AuditFailureBehavior.FailClosed;
})
.UsePostgreSql(options =>
{
    options.ConnectionFactory = () =>
        new NpgsqlConnection(builder.Configuration.GetConnectionString("AuditDb"));
    options.Schema = "audit";
    options.Table = "records";
});
```

### Option B: SQL Server / Azure SQL
```csharp
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.SqlServer;
using Microsoft.Data.SqlClient;

builder.Services.AddAuditing()
    .UseSqlServer(options =>
    {
        options.ConnectionFactory = () =>
            new SqlConnection(builder.Configuration.GetConnectionString("AuditDb"));
        options.Schema = "audit";
        options.Table = "records";
    });
```

### Option C: SQLite (Local Development & Edge)
```csharp
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Sqlite;
using Microsoft.Data.Sqlite;

builder.Services.AddAuditing()
    .UseSqlite(options =>
    {
        options.ConnectionFactory = () =>
            new SqliteConnection("Data Source=audit.db");
        options.Table = "audit_records";
    });
```

### Option D: In-Memory Testing
```csharp
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;

builder.Services.AddAuditing()
    .UseStore<InMemoryAuditStore>();
```

---

## 3. Emitting Your First Audit Record

Inject `IAuditStore` into your domain services or controller endpoints:

```csharp
using EricksonLopez.Auditing;

public class OrderService
{
    private readonly IAuditStore _auditStore;

    public OrderService(IAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    public async Task ApproveOrderAsync(string orderId, string tenantId, CancellationToken ct)
    {
        // 1. Execute business operation logic...

        // 2. Construct canonical audit record
        var record = new AuditRecord
        {
            Id = AuditId.NewId(), // Sequential RFC 9562 UUIDv7
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-4819", "alice@enterprise.com"),
            Action = AuditAction.Approve,
            Resource = new AuditResource("Order", orderId),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: tenantId,
                Source: "OrderService",
                CorrelationId: Guid.NewGuid().ToString("N")),
            Changes = new[]
            {
                new AuditChange("Status", "PendingApproval", "Approved"),
                new AuditChange("ApprovedAmount", "0.00", "1500.00"),
                AuditChange.Redacted("ApproverPin")
            }
        };

        // 3. Persist append-only audit record
        await _auditStore.AppendAsync(record, ct);
    }
}
```

---

## 4. Querying Audit Trails via Keyset Pagination

Query records efficiently without expensive database offsets:

```csharp
var query = new AuditQuery
{
    TenantId = "tenant-acme",
    ResourceType = "Order",
    Outcome = AuditOutcome.Success,
    PageSize = 50
};

AuditQueryResult result = await _auditStore.QueryAsync(query, cancellationToken);

foreach (AuditRecord entry in result.Records)
{
    Console.WriteLine($"[{entry.OccurredAt:O}] {entry.Actor.DisplayName} -> {entry.Action.Code} on {entry.Resource.Id}");
}

// Keyset cursor seek for next page:
if (result.HasMore)
{
    var nextPage = await _auditStore.QueryAsync(query with { AfterRecordId = result.NextCursorId }, cancellationToken);
}
```
