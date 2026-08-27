-- ─────────────────────────────────────────────────────────────────────────────
-- EricksonLopez.Auditing — MySQL 8.0+ / MariaDB 10.5+ Schema Migration
-- Version: 1.0.0
-- Description:
--   Creates the audit_records table and indexes optimized for tenant-scoped queries.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `audit_records` (
    `id`              VARCHAR(36)     NOT NULL,
    `occurred_at`     DATETIME(6)     NOT NULL,
    `tenant_id`       VARCHAR(128)    NOT NULL,
    `source`          VARCHAR(128)    NOT NULL,

    `actor_type`      TINYINT UNSIGNED NOT NULL,
    `actor_id`        VARCHAR(128)    NOT NULL,
    `actor_name`      VARCHAR(256)    NULL,

    `action_code`     VARCHAR(128)    NOT NULL,

    `resource_type`   VARCHAR(128)    NOT NULL,
    `resource_id`     VARCHAR(128)    NOT NULL,
    `aggregate_type`  VARCHAR(128)    NULL,
    `aggregate_id`    VARCHAR(128)    NULL,

    `outcome`         TINYINT UNSIGNED NOT NULL,
    `error_code`      VARCHAR(128)    NULL,

    `correlation_id`  VARCHAR(128)    NULL,
    `causation_id`    VARCHAR(128)    NULL,
    `request_id`      VARCHAR(128)    NULL,
    `ip_address`      VARCHAR(45)     NULL,
    `user_agent`      VARCHAR(512)    NULL,

    `changes`         JSON            NULL,

    `integrity_hash`  VARCHAR(128)    NULL,
    `previous_hash`   VARCHAR(128)    NULL,

    PRIMARY KEY (`occurred_at`, `id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── Indexes ───────────────────────────────────────────────────────────────────

CREATE INDEX `ix_audit_records_tenant_occurred`
    ON `audit_records` (`tenant_id`, `occurred_at` DESC);

CREATE INDEX `ix_audit_records_actor`
    ON `audit_records` (`tenant_id`, `actor_id`, `occurred_at` DESC);

CREATE INDEX `ix_audit_records_resource`
    ON `audit_records` (`tenant_id`, `resource_type`, `resource_id`, `occurred_at` DESC);

CREATE INDEX `ix_audit_records_correlation`
    ON `audit_records` (`correlation_id`);
