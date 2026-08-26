# ADR-0003: Database-Level Multi-Tenant Isolation (RLS / Session Context / VPD)

## Context

In multi-tenant SaaS applications, application-level `WHERE tenant_id = @TenantId` filtering is vulnerable to developer omission or SQL injection mistakes. Strong compliance standards demand that multi-tenancy be enforced natively at the database engine layer.

## Decision

Every `AuditRecord` requires a valid `TenantId` in `AuditContext` (or the reserved platform constant `AuditContext.SystemTenantId = "system"`). Storage adapters automatically execute database-native session configuration before running queries or batch inserts:
* **PostgreSQL:** `SELECT set_config('audit.tenant_id', @TenantId, false)` + `FORCE ROW LEVEL SECURITY`.
* **SQL Server:** `sp_set_session_context @key=N'TenantId', @value=@TenantId` + Security Policy.
* **Oracle:** `DBMS_SESSION.SET_IDENTIFIER(@TenantId)` + Virtual Private Database (VPD) policy.
* **MySQL:** `SET @audit_tenant_id = @TenantId`.

Batch operations enforce strict tenant homogeneity (`All records in a batch must belong to the same tenant`).

## Consequences

### Positive
* Accidental cross-tenant data leakage is prevented even if an application query lacks an explicit filter.
* Auditing infrastructure complies with strict enterprise SOC2/PCI-DSS multi-tenant data segregation rules.

### Negative / Trade-offs
* Batches containing mixed tenants must be grouped and dispatched per tenant.
