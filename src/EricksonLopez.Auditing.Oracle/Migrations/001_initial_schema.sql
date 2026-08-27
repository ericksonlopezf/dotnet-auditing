-- ─────────────────────────────────────────────────────────────────────────────
-- EricksonLopez.Auditing — Oracle Database 19c / 21c / 23ai Schema Migration
-- Version: 1.0.0
-- Description:
--   Creates the AUDIT_RECORDS table, composite indexes, and guidelines
--   for Virtual Private Database (VPD) tenant isolation.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE "AUDIT_RECORDS" (
    "ID"              VARCHAR2(36)            NOT NULL,
    "OCCURRED_AT"     TIMESTAMP WITH TIME ZONE NOT NULL,
    "TENANT_ID"       VARCHAR2(128)           NOT NULL,
    "SOURCE"          VARCHAR2(128)           NOT NULL,

    "ACTOR_TYPE"      NUMBER(3)               NOT NULL,
    "ACTOR_ID"        VARCHAR2(128)           NOT NULL,
    "ACTOR_NAME"      VARCHAR2(256)           NULL,

    "ACTION_CODE"     VARCHAR2(128)           NOT NULL,

    "RESOURCE_TYPE"   VARCHAR2(128)           NOT NULL,
    "RESOURCE_ID"     VARCHAR2(128)           NOT NULL,
    "AGGREGATE_TYPE"  VARCHAR2(128)           NULL,
    "AGGREGATE_ID"    VARCHAR2(128)           NULL,

    "OUTCOME"         NUMBER(3)               NOT NULL,
    "ERROR_CODE"      VARCHAR2(128)           NULL,

    "CORRELATION_ID"  VARCHAR2(128)           NULL,
    "CAUSATION_ID"    VARCHAR2(128)           NULL,
    "REQUEST_ID"      VARCHAR2(128)           NULL,
    "IP_ADDRESS"      VARCHAR2(45)            NULL,
    "USER_AGENT"      VARCHAR2(512)           NULL,

    "CHANGES"         CLOB                    NULL,

    "INTEGRITY_HASH"  VARCHAR2(128)           NULL,
    "PREVIOUS_HASH"   VARCHAR2(128)           NULL,

    CONSTRAINT "PK_AUDIT_RECORDS" PRIMARY KEY ("OCCURRED_AT", "ID")
);

-- ── Indexes ───────────────────────────────────────────────────────────────────

CREATE INDEX "IX_AUD_REC_TENANT_OCC"
    ON "AUDIT_RECORDS" ("TENANT_ID", "OCCURRED_AT" DESC);

CREATE INDEX "IX_AUD_REC_ACTOR"
    ON "AUDIT_RECORDS" ("TENANT_ID", "ACTOR_ID", "OCCURRED_AT" DESC);

CREATE INDEX "IX_AUD_REC_RESOURCE"
    ON "AUDIT_RECORDS" ("TENANT_ID", "RESOURCE_TYPE", "RESOURCE_ID", "OCCURRED_AT" DESC);

CREATE INDEX "IX_AUD_REC_CORRELATION"
    ON "AUDIT_RECORDS" ("CORRELATION_ID");
