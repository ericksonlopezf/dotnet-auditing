# Getting Started with EricksonLopez.Auditing

Complete step-by-step onboarding guide to integrating `EricksonLopez.Auditing` from scratch into a production-grade enterprise application.

---

## Step 1: Understanding the Canonical Data Model

Before writing code, familiarize yourself with the 6 foundational dimensions:

| Dimension | Role in Audit Evidence |
| :--- | :--- |
| **`AuditRecord`** | The immutable root record representing a completed business or security event. |
| **`AuditActor`** | Identifies who performed the action (`User`, `Service`, or `System`). |
| **`AuditAction`** | Identifies what operation was executed (extensible value object). |
| **`AuditResource`** | The entity or aggregate root impacted by the operation. |
| **`AuditContext`** | Technical environment data: `TenantId`, `Source`, `CorrelationId`, `RequestId`, `IpAddress`. |
| **`AuditOutcome`** | The result of the action: `Success`, `Failure`, `Denied`, `Cancelled`, or `Partial`. |

---

## Step 2: Choosing Your Persistence Provider

| Scenario / Tech Stack | Recommended Adapter Package |
| :--- | :--- |
| Production enterprise, PostgreSQL RLS, compliance | `EricksonLopez.Auditing.PostgreSql` |
| Azure SQL / SQL Server enterprise environments | `EricksonLopez.Auditing.SqlServer` |
| Microservices on MySQL 8.0+ / MariaDB | `EricksonLopez.Auditing.MySql` |
| Oracle Database 19c/21c/23ai with VPD | `EricksonLopez.Auditing.Oracle` |
| Edge computing, IoT, desktop apps, local unit tests | `EricksonLopez.Auditing.Sqlite` |
| Document / NoSQL applications on MongoDB | `EricksonLopez.Auditing.MongoDb` |
| Applications with existing EF Core infrastructure | `EricksonLopez.Auditing.EntityFrameworkCore` |
| Any ADO.NET compatible ANSI SQL database | `EricksonLopez.Auditing.Dapper` |
| Fast in-memory unit tests | `EricksonLopez.Auditing.Testing` |

---

## Step 3: Installing NuGet Packages

```bash
# Core pipeline & abstractions
dotnet add package EricksonLopez.Auditing

# Selected storage adapter (e.g. PostgreSQL)
dotnet add package EricksonLopez.Auditing.PostgreSql

# Optional: Distributed tracing & metrics
dotnet add package EricksonLopez.Auditing.OpenTelemetry
```

---

## Step 4: Configuring Dependency Injection

In `Program.cs`:

```csharp
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.PostgreSql;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuditing(cfg =>
{
    // FailClosed ensures critical operations fail if audit evidence cannot be persisted
    cfg.DefaultFailureBehavior = AuditFailureBehavior.FailClosed;

    // Sanitize proprietary sensitive fields
    cfg.GlobalFieldDenylist.Add("InternalToken");
    cfg.GlobalFieldDenylist.Add("BankSecretKey");
})
.UsePostgreSql(options =>
{
    options.ConnectionFactory = () =>
        new NpgsqlConnection(builder.Configuration.GetConnectionString("AuditDatabase"));
    options.Schema = "audit";
    options.Table = "records";
});
```

---

## Step 5: Applying Database Migrations

Each relational adapter includes an official SQL DDL script in its `Migrations/` directory:

* **PostgreSQL:** `src/EricksonLopez.Auditing.PostgreSql/Migrations/001_initial_schema.sql`
* **SQL Server:** `src/EricksonLopez.Auditing.SqlServer/Migrations/001_initial_schema.sql`
* **MySQL:** `src/EricksonLopez.Auditing.MySql/Migrations/001_initial_schema.sql`
* **Oracle:** `src/EricksonLopez.Auditing.Oracle/Migrations/001_initial_schema.sql`
* **SQLite:** `src/EricksonLopez.Auditing.Sqlite/Migrations/001_initial_schema.sql`

For PostgreSQL:
```sql
CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE IF NOT EXISTS audit.records (
    id UUID NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    tenant_id TEXT NOT NULL,
    actor_type INT NOT NULL,
    actor_id TEXT NOT NULL,
    actor_name TEXT,
    action_code TEXT NOT NULL,
    resource_type TEXT NOT NULL,
    resource_id TEXT NOT NULL,
    aggregate_type TEXT,
    aggregate_id TEXT,
    outcome INT NOT NULL,
    source TEXT NOT NULL,
    correlation_id TEXT,
    causation_id TEXT,
    request_id TEXT,
    ip_address TEXT,
    user_agent TEXT,
    error_code TEXT,
    changes_json JSONB,
    integrity_hash TEXT,
    previous_hash TEXT,
    PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

ALTER TABLE audit.records ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit.records FORCE ROW LEVEL SECURITY;

CREATE POLICY audit_tenant_isolation ON audit.records
    FOR ALL
    USING (tenant_id = current_setting('audit.tenant_id', true));
```

---

## Step 6: Automatic Identity Resolution with `IAuditActorProvider`

Implement `IAuditActorProvider` to automatically extract the authenticated user from `HttpContext`:

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

// Register in DI:
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuditing()
    .UseActorProvider<HttpContextAuditActorProvider>()
    .UsePostgreSql(opts => ...);
```

---

## Step 7: Ambient Scope & Metadata Enrichment

Use `AuditScope` to enrich multi-step workflows without passing parameters through every method:

```csharp
using (var scope = AuditScope.Begin())
{
    scope.WithMetadata("WorkflowId", "wf-9812")
         .WithMetadata("ExecutionMode", "AutomatedBatch");

    await ProcessPaymentsAsync();
}
```
