-- ─────────────────────────────────────────────────────────────────────────────
-- EricksonLopez.Auditing — SQLite Schema Migration
-- Version: 1.0.0
-- Description:
--   Creates the audit_records table and indexes for fast tenant/time queries.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS audit_records (
    id              TEXT    NOT NULL,
    occurred_at     TEXT    NOT NULL,
    tenant_id       TEXT    NOT NULL,
    source          TEXT    NOT NULL,

    actor_type      INTEGER NOT NULL,
    actor_id        TEXT    NOT NULL,
    actor_name      TEXT    NULL,

    action_code     TEXT    NOT NULL,

    resource_type   TEXT    NOT NULL,
    resource_id     TEXT    NOT NULL,
    aggregate_type  TEXT    NULL,
    aggregate_id    TEXT    NULL,

    outcome         INTEGER NOT NULL,
    error_code      TEXT    NULL,

    correlation_id  TEXT    NULL,
    causation_id    TEXT    NULL,
    request_id      TEXT    NULL,
    ip_address      TEXT    NULL,
    user_agent      TEXT    NULL,

    changes         TEXT    NULL,

    integrity_hash  TEXT    NULL,
    previous_hash   TEXT    NULL,

    PRIMARY KEY (occurred_at ASC, id ASC)
);

CREATE INDEX IF NOT EXISTS ix_audit_records_tenant_occurred
    ON audit_records (tenant_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_records_actor
    ON audit_records (tenant_id, actor_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_records_resource
    ON audit_records (tenant_id, resource_type, resource_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_records_correlation
    ON audit_records (correlation_id)
    WHERE correlation_id IS NOT NULL;
