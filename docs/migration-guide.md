<!-- Copyright © Erickson Lopez. MIT License. -->
# Migration & Database Provisioning Guide: EricksonLopez.Auditing

Provisioning instructions, database schema DDL scripts, and version migration policies for `EricksonLopez.Auditing`.

---

## Release Status: Version 2.0.0 (Release Date: 2026-09-23)

`EricksonLopez.Auditing` **v2.0.0** introduces significant architectural, security, and cryptographic enhancements. In accordance with [Semantic Versioning 2.0.0](https://semver.org/), this major release introduces breaking changes compared to `v1.0.0`.

This guide provides exhaustive step-by-step instructions to upgrade application code and database schemas from `v1.0.0` to `v2.0.0`.

---

## Migrating from v1.0.0 to v2.0.0

### 1. C# API & Architectural Breaking Changes

#### A. Strongly-Typed `TenantId` Value Object
* **v1.0.0:** Tenant identifiers were represented as standard `string` primitives in `AuditContext.TenantId`, `AuditQuery.TenantId`, and `IAuditIntegrityProvider.GetCurrentKey(string tenantId)`.
* **v2.0.0:** Replaced with the strongly-typed `TenantId` readonly struct value object to prevent primitive obsession and cross-tenant parameter confusion.
* **Migration Action:**
  ```csharp
  // v1.0.0
  var context = new AuditContext("tenant-corp-1", "BillingService", "corr-123");
  var query = new AuditQuery { TenantId = "tenant-corp-1" };

  // v2.0.0
  var tenant = new TenantId("tenant-corp-1"); // or (TenantId)"tenant-corp-1" via explicit operator
  var context = new AuditContext(tenant, "BillingService", "corr-123");
  var query = new AuditQuery { TenantId = tenant };
  ```

#### B. `AuditContext.SystemTenantId` Constant to Static Readonly Field
* **v1.0.0:** `public const string SystemTenantId = "system";`
* **v2.0.0:** `public static readonly TenantId SystemTenantId = new TenantId("system");`
* **Migration Action:**
  Replace compile-time constant `switch/case` patterns with `if/else` or pattern matching `when`:
  ```csharp
  // v1.0.0
  switch (context.TenantId)
  {
      case AuditContext.SystemTenantId:
          HandleSystemAudit();
          break;
  }

  // v2.0.0
  if (context.TenantId == AuditContext.SystemTenantId)
  {
      HandleSystemAudit();
  }
  // Or with pattern matching:
  switch (context.TenantId)
  {
      case var t when t == AuditContext.SystemTenantId:
          HandleSystemAudit();
          break;
  }
  ```

#### C. Keyset Pagination Cursors (`AfterRecordId` & `NextCursorId` Removed)
* **v1.0.0:** Keyset pagination relied on `Guid? AfterRecordId` on `AuditQuery` and `Guid? NextCursorId` on `AuditQueryResult`.
* **v2.0.0:** Replaced with opaque, high-performance base64 composite tokens: `AuditQuery.ContinuationToken` (`string?`) and `AuditQueryResult.NextPageToken` (`string?`).
* **Migration Action:**
  ```csharp
  // v1.0.0
  var result = await store.QueryAsync(new AuditQuery { AfterRecordId = lastGuid, PageSize = 50 });
  Guid? nextCursor = result.NextCursorId;

  // v2.0.0
  var result = await store.QueryAsync(new AuditQuery { ContinuationToken = nextToken, PageSize = 50 });
  string? nextToken = result.NextPageToken;
  ```

#### D. `AuditSensitivityPipeline`: Synchronous `Apply` Removed
* **v1.0.0:** Synchronous `public IReadOnlyList<AuditChange>? Apply(IReadOnlyList<AuditChange>? changes)`.
* **v2.0.0:** Pipeline is asynchronous and tenant-aware: `ApplyAsync(IReadOnlyList<AuditChange>? changes, string tenantId, CancellationToken ct)` and `SanitizeAsync(AuditRecord record, CancellationToken ct)`.
* **Migration Action:**
  ```csharp
  // v1.0.0
  var sanitizedChanges = pipeline.Apply(rawChanges);

  // v2.0.0
  var sanitizedChanges = await pipeline.ApplyAsync(rawChanges, tenantId, cancellationToken);
  // Or sanitize full record:
  var sanitizedRecord = await pipeline.SanitizeAsync(rawRecord, cancellationToken);
  ```

#### E. Strict Constructor Validation (Fail-Fast)
* **v1.0.0:** Permitted null or whitespace values in actor, resource, and context identifiers.
* **v2.0.0:** Enforces `ArgumentException.ThrowIfNullOrWhiteSpace` on `AuditActor.Id`, `AuditResource.Type`, `AuditResource.Id`, and `AuditContext.Source`.
* **Migration Action:** Ensure inputs are non-empty and trimmed prior to instantiating audit primitives.

#### F. String Length Enforcement & Automatic Truncation
* **v1.0.0:** Unbounded string values were persisted as-is.
* **v2.0.0:** `AuditChange.OldValue` and `AuditChange.NewValue` are bounded by `AuditConfiguration.MaxStringLength` (default: 4000 characters). Values exceeding this length are truncated and suffixed with `"[TRUNCATED]"`.

#### G. Cryptographic Integrity Chain Verification
* **v1.0.0:** Hash computation used a simple 9-field pipe delimiter without change payload verification.
* **v2.0.0:** `HmacAuditIntegrityService` enforces a comprehensive canonical representation incorporating all record fields and changes.
* **Migration Action:** For auditing systems with historical v1.0.0 records, maintain a dual-verification strategy checking v2 format first, falling back to legacy v1 verification if the record was generated prior to the v2.0.0 migration date.

---

### 2. Database Migration Scripts (v1.0.0 → v2.0.0)

Apply the script corresponding to your database engine to upgrade existing schemas created with v1.0.0:

#### PostgreSQL (v1.0.0 → v2.0.0)
```sql
-- 1. Add idempotency_key column
ALTER TABLE audit.records 
    ADD COLUMN IF NOT EXISTS idempotency_key TEXT;

-- 2. Add filtered unique index for cryptographic chain integrity
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS idx_audit_records_prev_hash
    ON audit.records (tenant_id, previous_hash)
    WHERE previous_hash IS NOT NULL;
```

#### Microsoft SQL Server (v1.0.0 → v2.0.0)
```sql
-- 1. Add idempotency_key column
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('audit.records') AND name = 'idempotency_key'
)
BEGIN
    ALTER TABLE audit.records ADD idempotency_key NVARCHAR(128) NULL;
END;

-- 2. Add filtered unique index for cryptographic chain integrity
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID('audit.records') AND name = 'uidx_audit_records_prev_hash'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX uidx_audit_records_prev_hash
        ON audit.records (tenant_id, previous_hash)
        WHERE previous_hash IS NOT NULL;
END;
```

#### SQLite (v1.0.0 → v2.0.0)
```sql
-- 1. Add idempotency_key column
ALTER TABLE audit_records ADD COLUMN idempotency_key TEXT;

-- 2. Add filtered unique index for cryptographic chain integrity
CREATE UNIQUE INDEX IF NOT EXISTS idx_audit_records_prev_hash
    ON audit_records (tenant_id, previous_hash)
    WHERE previous_hash IS NOT NULL;
```

#### MySQL / MariaDB (v1.0.0 → v2.0.0)
```sql
-- 1. Add idempotency_key column
ALTER TABLE audit_records 
    ADD COLUMN idempotency_key VARCHAR(128) NULL AFTER correlation_id;

-- 2. Add unique index for cryptographic chain integrity
CREATE UNIQUE INDEX idx_audit_records_prev_hash
    ON audit_records (tenant_id, previous_hash);
```

#### Oracle Database (v1.0.0 → v2.0.0)
```sql
-- 1. Add idempotency_key column
ALTER TABLE audit_records 
    ADD (idempotency_key VARCHAR2(128) NULL);

-- 2. Add unique index for cryptographic chain integrity
CREATE UNIQUE INDEX uidx_audit_records_prev_hash
    ON audit_records (tenant_id, previous_hash);
```

---

## Fresh Database Schema Provisioning (v2.0.0)

When deploying a fresh database instance for `EricksonLopez.Auditing` v2.0.0, use the following production-grade DDL scripts:

### 1. PostgreSQL (with Row-Level Security)

```sql
-- EricksonLopez.Auditing DDL Schema
-- Provider: PostgreSQL 14+
-- Version: 2.0.0

CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE IF NOT EXISTS audit.records (
    id UUID PRIMARY KEY,
    occurred_at TIMESTAMPTZ NOT NULL,
    tenant_id TEXT NOT NULL,
    actor_type SMALLINT NOT NULL,
    actor_id TEXT NOT NULL,
    actor_name TEXT,
    action TEXT NOT NULL,
    resource_type TEXT NOT NULL,
    resource_id TEXT NOT NULL,
    resource_name TEXT,
    outcome SMALLINT NOT NULL,
    source TEXT NOT NULL,
    correlation_id TEXT,
    causation_id TEXT,
    request_id TEXT,
    idempotency_key TEXT,
    ip_address TEXT,
    user_agent TEXT,
    error_code TEXT,
    changes_json JSONB,
    metadata_json JSONB,
    integrity_hash TEXT,
    previous_hash TEXT
);

-- Enable PostgreSQL Row-Level Security for multi-tenant isolation
ALTER TABLE audit.records ENABLE ROW LEVEL SECURITY;

CREATE POLICY audit_tenant_isolation ON audit.records
    FOR ALL
    USING (tenant_id = current_setting('audit.tenant_id', true))
    WITH CHECK (tenant_id = current_setting('audit.tenant_id', true));

-- Composite index for O(1) Keyset seek pagination
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_audit_records_tenant_time_id
    ON audit.records (tenant_id, occurred_at DESC, id DESC);

-- Filtered unique index for cryptographic hash chain integrity
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS idx_audit_records_prev_hash
    ON audit.records (tenant_id, previous_hash)
    WHERE previous_hash IS NOT NULL;
```

---

### 2. Microsoft SQL Server / Azure SQL (with `SESSION_CONTEXT`)

```sql
-- EricksonLopez.Auditing DDL Schema
-- Provider: Microsoft SQL Server 2016+ / Azure SQL Database
-- Version: 2.0.0

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
    EXEC('CREATE SCHEMA audit');

CREATE TABLE audit.records (
    id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY NONCLUSTERED,
    occurred_at DATETIMEOFFSET(7) NOT NULL,
    tenant_id NVARCHAR(128) NOT NULL,
    actor_type TINYINT NOT NULL,
    actor_id NVARCHAR(256) NOT NULL,
    actor_name NVARCHAR(256) NULL,
    action NVARCHAR(64) NOT NULL,
    resource_type NVARCHAR(128) NOT NULL,
    resource_id NVARCHAR(256) NOT NULL,
    resource_name NVARCHAR(256) NULL,
    outcome TINYINT NOT NULL,
    source NVARCHAR(128) NOT NULL,
    correlation_id NVARCHAR(128) NULL,
    causation_id NVARCHAR(128) NULL,
    request_id NVARCHAR(128) NULL,
    idempotency_key NVARCHAR(128) NULL,
    ip_address NVARCHAR(64) NULL,
    user_agent NVARCHAR(512) NULL,
    error_code NVARCHAR(64) NULL,
    changes_json NVARCHAR(MAX) NULL,
    metadata_json NVARCHAR(MAX) NULL,
    integrity_hash NVARCHAR(128) NULL,
    previous_hash NVARCHAR(128) NULL
);

-- Clustered composite index for O(1) Keyset pagination
CREATE CLUSTERED INDEX cidx_audit_records_tenant_time_id
    ON audit.records (tenant_id, occurred_at DESC, id DESC);

-- Filtered unique index for cryptographic hash chain integrity
CREATE UNIQUE NONCLUSTERED INDEX uidx_audit_records_prev_hash
    ON audit.records (tenant_id, previous_hash)
    WHERE previous_hash IS NOT NULL;
```

---

### 3. SQLite (WAL Mode)

```sql
-- EricksonLopez.Auditing DDL Schema
-- Provider: SQLite 3.38+
-- Version: 2.0.0

CREATE TABLE IF NOT EXISTS audit_records (
    id TEXT PRIMARY KEY,
    occurred_at TEXT NOT NULL,
    tenant_id TEXT NOT NULL,
    actor_type INTEGER NOT NULL,
    actor_id TEXT NOT NULL,
    actor_name TEXT,
    action TEXT NOT NULL,
    resource_type TEXT NOT NULL,
    resource_id TEXT NOT NULL,
    resource_name TEXT,
    outcome INTEGER NOT NULL,
    source TEXT NOT NULL,
    correlation_id TEXT,
    causation_id TEXT,
    request_id TEXT,
    idempotency_key TEXT,
    ip_address TEXT,
    user_agent TEXT,
    error_code TEXT,
    changes_json TEXT,
    metadata_json TEXT,
    integrity_hash TEXT,
    previous_hash TEXT
);

CREATE INDEX IF NOT EXISTS idx_audit_records_tenant_time_id
    ON audit_records (tenant_id, occurred_at DESC, id DESC);

CREATE UNIQUE INDEX IF NOT EXISTS idx_audit_records_prev_hash
    ON audit_records (tenant_id, previous_hash)
    WHERE previous_hash IS NOT NULL;
```

---

### 4. MySQL / MariaDB

```sql
-- EricksonLopez.Auditing DDL Schema
-- Provider: MySQL 8.0+ / MariaDB 10.6+
-- Version: 2.0.0

CREATE TABLE IF NOT EXISTS audit_records (
    id CHAR(36) NOT NULL,
    occurred_at DATETIME(6) NOT NULL,
    tenant_id VARCHAR(128) NOT NULL,
    actor_type TINYINT NOT NULL,
    actor_id VARCHAR(256) NOT NULL,
    actor_name VARCHAR(256) NULL,
    action VARCHAR(64) NOT NULL,
    resource_type VARCHAR(128) NOT NULL,
    resource_id VARCHAR(256) NOT NULL,
    resource_name VARCHAR(256) NULL,
    outcome TINYINT NOT NULL,
    source VARCHAR(128) NOT NULL,
    correlation_id VARCHAR(128) NULL,
    causation_id VARCHAR(128) NULL,
    request_id VARCHAR(128) NULL,
    idempotency_key VARCHAR(128) NULL,
    ip_address VARCHAR(64) NULL,
    user_agent VARCHAR(512) NULL,
    error_code VARCHAR(64) NULL,
    changes_json JSON NULL,
    metadata_json JSON NULL,
    integrity_hash VARCHAR(128) NULL,
    previous_hash VARCHAR(128) NULL,
    PRIMARY KEY (id),
    INDEX idx_audit_records_tenant_time_id (tenant_id, occurred_at DESC, id DESC),
    UNIQUE INDEX idx_audit_records_prev_hash (tenant_id, previous_hash)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

---

### 5. Oracle Database (with Virtual Private Database)

```sql
-- EricksonLopez.Auditing DDL Schema
-- Provider: Oracle Database 19c / 21c / 23ai
-- Version: 2.0.0

CREATE TABLE audit_records (
    id RAW(16) NOT NULL,
    occurred_at TIMESTAMP WITH TIME ZONE NOT NULL,
    tenant_id VARCHAR2(128) NOT NULL,
    actor_type NUMBER(3) NOT NULL,
    actor_id VARCHAR2(256) NOT NULL,
    actor_name VARCHAR2(256),
    action VARCHAR2(64) NOT NULL,
    resource_type VARCHAR2(128) NOT NULL,
    resource_id VARCHAR2(256) NOT NULL,
    resource_name VARCHAR2(256),
    outcome NUMBER(3) NOT NULL,
    source VARCHAR2(128) NOT NULL,
    correlation_id VARCHAR2(128),
    causation_id VARCHAR2(128),
    request_id VARCHAR2(128),
    idempotency_key VARCHAR2(128) NULL,
    ip_address VARCHAR2(64),
    user_agent VARCHAR2(512),
    error_code VARCHAR2(64),
    changes_json CLOB,
    metadata_json CLOB,
    integrity_hash VARCHAR2(128),
    previous_hash VARCHAR2(128),
    CONSTRAINT pk_audit_records PRIMARY KEY (id)
);

CREATE INDEX idx_audit_tenant_time_id
    ON audit_records (tenant_id, occurred_at DESC, id DESC);

CREATE UNIQUE INDEX uidx_audit_records_prev_hash
    ON audit_records (tenant_id, previous_hash);
```

---

## Adoption Checklist (v2.0.0)

```text
[ ] Update NuGet packages to v2.0.0: dotnet add package EricksonLopez.Auditing --version 2.0.0
[ ] Update storage adapter packages to v2.0.0 (e.g. EricksonLopez.Auditing.PostgreSql --version 2.0.0)
[ ] Migrate codebase from string TenantId to strongly-typed TenantId value object
[ ] Migrate keyset pagination code to use ContinuationToken and NextPageToken
[ ] Update any switch statements evaluating AuditContext.SystemTenantId
[ ] Apply database ALTER TABLE migration scripts to existing databases (adding idempotency_key and unique previous_hash index)
[ ] Verify non-whitespace inputs for AuditActor.Id, AuditResource.Id, AuditResource.Type, AuditContext.Source
[ ] Verify compilation: dotnet build -c Release
[ ] Run full test suite: dotnet test -c Release
```
