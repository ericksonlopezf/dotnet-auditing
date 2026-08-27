# Migration & Database Provisioning Guide: EricksonLopez.Auditing

Initial provisioning instructions, database schema DDL scripts, and version migration policies for `EricksonLopez.Auditing`.

---

## Release Status: Version 1.0.0 (Inaugural GA Release)

`EricksonLopez.Auditing` **v1.0.0** is the foundational initial release of the ecosystem.

* Because this is the inaugural release, there are no prior versions to migrate from.
* As future major versions ($v2.0.0$, $v3.0.0$, etc.) are developed in accordance with [Semantic Versioning 2.0.0](https://semver.org/), breaking changes, signature updates, and upgrade steps will be cataloged in this guide.

---

## Initial Database Schema Provisioning (v1.0.0)

When deploying `EricksonLopez.Auditing` v1.0.0 with a persistent storage adapter, apply the corresponding database schema DDL script.

### 1. PostgreSQL (with Row-Level Security)

```sql
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
    USING (tenant_id = current_setting('app.current_tenant', true))
    WITH CHECK (tenant_id = current_setting('app.current_tenant', true));

-- Composite index for O(1) Keyset seek pagination
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_audit_records_tenant_time_id
    ON audit.records (tenant_id, occurred_at DESC, id DESC);
```

---

### 2. Microsoft SQL Server / Azure SQL (with `SESSION_CONTEXT`)

```sql
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
```

---

### 3. SQLite (WAL Mode)

```sql
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
```

---

### 4. MySQL / MariaDB

```sql
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
    ip_address VARCHAR(64) NULL,
    user_agent VARCHAR(512) NULL,
    error_code VARCHAR(64) NULL,
    changes_json JSON NULL,
    metadata_json JSON NULL,
    integrity_hash VARCHAR(128) NULL,
    previous_hash VARCHAR(128) NULL,
    PRIMARY KEY (id),
    INDEX idx_audit_records_tenant_time_id (tenant_id, occurred_at DESC, id DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

---

### 5. Oracle Database (with Virtual Private Database)

```sql
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
```

---

## Adoption Checklist (v1.0.0)

```text
[ ] Add package reference: dotnet add package EricksonLopez.Auditing
[ ] Add storage adapter (e.g. dotnet add package EricksonLopez.Auditing.PostgreSql)
[ ] Execute database DDL schema initialization
[ ] Register in DI: services.AddAuditing(cfg => { ... }).UsePostgreSql(connString);
[ ] Verify compilation: dotnet build -c Release
[ ] Run test suite: dotnet test
```

