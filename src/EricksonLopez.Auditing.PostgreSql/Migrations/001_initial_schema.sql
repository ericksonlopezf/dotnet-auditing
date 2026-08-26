-- ─────────────────────────────────────────────────────────────────────────────
-- EricksonLopez.Auditing — PostgreSQL Schema Migration
-- Version: 1.0.0
-- Description:
--   Creates the audit schema, the partitioned records table, RLS policies,
--   indexes optimized for tenant-scoped queries, and an initial monthly partition.
--
-- Requirements:
--   PostgreSQL 14+ (for FORCE ROW LEVEL SECURITY and partition improvements)
--   pg_partman (optional, for automated partition management)
--
-- Usage:
--   Apply once per environment. Safe to run repeatedly if guards are in place.
--   For partition management, install pg_partman and configure a cron job.
-- ─────────────────────────────────────────────────────────────────────────────

BEGIN;

-- ── Schema ────────────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS audit;

-- ── Application roles ─────────────────────────────────────────────────────────

-- Role used by the application (write + read, tenant-scoped via RLS)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'audit_app_role') THEN
        CREATE ROLE audit_app_role NOLOGIN;
    END IF;
END $$;

-- Role used by compliance officers or audit readers (cross-tenant read, no write)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'audit_reader_role') THEN
        CREATE ROLE audit_reader_role NOLOGIN;
    END IF;
END $$;

-- ── Records table (partitioned by occurred_at range) ──────────────────────────

CREATE TABLE IF NOT EXISTS audit.records (
    -- Identity
    id              UUID            NOT NULL,
    occurred_at     TIMESTAMPTZ     NOT NULL,

    -- Tenant
    tenant_id       TEXT            NOT NULL,

    -- Context
    source          TEXT            NOT NULL,
    correlation_id  TEXT,
    causation_id    TEXT,
    request_id      TEXT,
    ip_address      INET,
    user_agent      TEXT,

    -- Actor
    actor_type      SMALLINT        NOT NULL,   -- AuditActorType enum value
    actor_id        TEXT            NOT NULL,
    actor_name      TEXT,                       -- PII — subject to pseudonymization

    -- Action
    action_code     TEXT            NOT NULL,

    -- Resource
    resource_type   TEXT            NOT NULL,
    resource_id     TEXT            NOT NULL,
    aggregate_type  TEXT,
    aggregate_id    TEXT,

    -- Outcome
    outcome         SMALLINT        NOT NULL,   -- AuditOutcome enum value
    error_code      TEXT,                       -- Non-sensitive error category only

    -- Changes (JSONB — flexible, queryable)
    changes         JSONB,

    -- Integrity chain
    integrity_hash  TEXT,           -- HMAC-SHA256 of this record + previous_hash
    previous_hash   TEXT,           -- IntegrityHash of the preceding record in tenant chain

    CONSTRAINT pk_audit_records PRIMARY KEY (id, occurred_at)  -- partition key must be in PK
)
PARTITION BY RANGE (occurred_at);

-- ── Initial partition (current month) ────────────────────────────────────────

-- This creates an initial partition. In production, use pg_partman to automate this.
CREATE TABLE IF NOT EXISTS audit.records_2026_08
    PARTITION OF audit.records
    FOR VALUES FROM ('2026-08-01 00:00:00+00') TO ('2026-09-01 00:00:00+00');

-- ── Indexes ───────────────────────────────────────────────────────────────────

-- Primary query pattern: tenant + time range (all audit queries filter by tenant first)
CREATE INDEX IF NOT EXISTS ix_audit_records_tenant_occurred
    ON audit.records (tenant_id, occurred_at DESC);

-- Actor-based queries within a tenant
CREATE INDEX IF NOT EXISTS ix_audit_records_actor
    ON audit.records (tenant_id, actor_id, occurred_at DESC);

-- Resource-based queries within a tenant
CREATE INDEX IF NOT EXISTS ix_audit_records_resource
    ON audit.records (tenant_id, resource_type, resource_id, occurred_at DESC);

-- Correlation-based trace queries (sparse index — only non-null values)
CREATE INDEX IF NOT EXISTS ix_audit_records_correlation
    ON audit.records (correlation_id)
    WHERE correlation_id IS NOT NULL;

-- Outcome + action for security/compliance dashboards
CREATE INDEX IF NOT EXISTS ix_audit_records_action_outcome
    ON audit.records (tenant_id, action_code, outcome, occurred_at DESC);

-- ── Row-Level Security ────────────────────────────────────────────────────────

ALTER TABLE audit.records ENABLE ROW LEVEL SECURITY;

-- FORCE applies RLS even to table owners (prevents privilege escalation bypass)
ALTER TABLE audit.records FORCE ROW LEVEL SECURITY;

-- Application role: can only see and write records for the current tenant session.
-- The application MUST execute: SET LOCAL audit.tenant_id = '<tenant-id>'
-- within a transaction before any DML, or: SET audit.tenant_id = '<tenant-id>'
-- before any query (session-scoped).
DROP POLICY IF EXISTS audit_app_tenant_isolation ON audit.records;
CREATE POLICY audit_app_tenant_isolation
    ON audit.records
    FOR ALL
    TO audit_app_role
    USING (tenant_id = current_setting('audit.tenant_id', true));

-- Application INSERT policy: only allow inserting for the configured tenant
DROP POLICY IF EXISTS audit_app_insert ON audit.records;
CREATE POLICY audit_app_insert
    ON audit.records
    FOR INSERT
    TO audit_app_role
    WITH CHECK (tenant_id = current_setting('audit.tenant_id', true));

-- Reader role: cross-tenant SELECT only, no DML
DROP POLICY IF EXISTS audit_reader_cross_tenant ON audit.records;
CREATE POLICY audit_reader_cross_tenant
    ON audit.records
    FOR SELECT
    TO audit_reader_role
    USING (true);  -- cross-tenant read authorized for this role

-- ── Permissions ───────────────────────────────────────────────────────────────

GRANT USAGE ON SCHEMA audit TO audit_app_role, audit_reader_role;
GRANT SELECT, INSERT ON audit.records TO audit_app_role;
GRANT SELECT ON audit.records TO audit_reader_role;

-- ── Comments ─────────────────────────────────────────────────────────────────

COMMENT ON TABLE audit.records IS
    'Append-only audit trail records. Protected by RLS. Partitioned monthly by occurred_at. '
    'Never UPDATE or DELETE from this table directly — use documented redaction procedures.';

COMMENT ON COLUMN audit.records.tenant_id IS
    'Tenant scope enforced by RLS policy audit_app_tenant_isolation. '
    'Use reserved value ''system'' for platform-level events.';

COMMENT ON COLUMN audit.records.actor_name IS
    'PII — subject to GDPR pseudonymization. May be nullified via documented redaction procedure '
    'without deleting the record.';

COMMENT ON COLUMN audit.records.ip_address IS
    'PII under GDPR. Store only when required by compliance policy. '
    'Consider pseudonymization or exclusion based on privacy impact assessment.';

COMMENT ON COLUMN audit.records.changes IS
    'JSONB array of field-level changes. Sensitive fields are stored as redacted entries '
    '(IsRedacted=true) with null values — never the actual sensitive content.';

COMMENT ON COLUMN audit.records.integrity_hash IS
    'HMAC-SHA256(canonical_record || previous_hash). Computed by the application layer. '
    'Null when integrity chain is disabled. Used to detect record tampering.';

COMMENT ON COLUMN audit.records.previous_hash IS
    'The integrity_hash of the immediately preceding record in this tenant''s chain. '
    'Null for the first record in a chain segment (e.g., after key rotation).';

COMMIT;
