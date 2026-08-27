-- ─────────────────────────────────────────────────────────────────────────────
-- EricksonLopez.Auditing — Microsoft SQL Server / Azure SQL Schema Migration
-- Version: 1.0.0
-- Description:
--   Creates the audit schema, the records table, security predicate function,
--   and Security Policy for Row-Level Security (RLS) via SESSION_CONTEXT.
-- ─────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'audit')
BEGIN
    EXEC('CREATE SCHEMA [audit];');
END
GO

-- ── Records Table ────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'audit' AND t.name = N'records')
BEGIN
    CREATE TABLE [audit].[records] (
        [id]              UNIQUEIDENTIFIER NOT NULL,
        [occurred_at]     DATETIMEOFFSET   NOT NULL,
        [tenant_id]       NVARCHAR(128)    NOT NULL,
        [source]          NVARCHAR(128)    NOT NULL,

        [actor_type]      TINYINT          NOT NULL,
        [actor_id]        NVARCHAR(128)    NOT NULL,
        [actor_name]      NVARCHAR(256)    NULL,

        [action_code]     NVARCHAR(128)    NOT NULL,

        [resource_type]   NVARCHAR(128)    NOT NULL,
        [resource_id]     NVARCHAR(128)    NOT NULL,
        [aggregate_type]  NVARCHAR(128)    NULL,
        [aggregate_id]    NVARCHAR(128)    NULL,

        [outcome]         TINYINT          NOT NULL,
        [error_code]      NVARCHAR(128)    NULL,

        [correlation_id]  NVARCHAR(128)    NULL,
        [causation_id]    NVARCHAR(128)    NULL,
        [request_id]      NVARCHAR(128)    NULL,
        [ip_address]      VARCHAR(45)      NULL,
        [user_agent]      NVARCHAR(512)    NULL,

        [changes]         NVARCHAR(MAX)    NULL,

        [integrity_hash]  VARCHAR(128)     NULL,
        [previous_hash]   VARCHAR(128)     NULL,

        CONSTRAINT [PK_audit_records] PRIMARY KEY CLUSTERED ([occurred_at] ASC, [id] ASC)
    );
END
GO

-- ── Indexes ───────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_audit_records_tenant_occurred' AND object_id = OBJECT_ID(N'[audit].[records]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_audit_records_tenant_occurred]
        ON [audit].[records] ([tenant_id] ASC, [occurred_at] DESC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_audit_records_actor' AND object_id = OBJECT_ID(N'[audit].[records]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_audit_records_actor]
        ON [audit].[records] ([tenant_id] ASC, [actor_id] ASC, [occurred_at] DESC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_audit_records_resource' AND object_id = OBJECT_ID(N'[audit].[records]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_audit_records_resource]
        ON [audit].[records] ([tenant_id] ASC, [resource_type] ASC, [resource_id] ASC, [occurred_at] DESC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_audit_records_correlation' AND object_id = OBJECT_ID(N'[audit].[records]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_audit_records_correlation]
        ON [audit].[records] ([correlation_id] ASC)
        WHERE [correlation_id] IS NOT NULL;
END
GO

-- ── Row-Level Security (RLS) Predicate Function & Policy ─────────────────────

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[fn_AuditTenantSecurityPredicate]') AND type = N'IF')
BEGIN
    DROP FUNCTION [audit].[fn_AuditTenantSecurityPredicate];
END
GO

CREATE FUNCTION [audit].[fn_AuditTenantSecurityPredicate](@TenantId NVARCHAR(128))
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN (
    SELECT 1 AS [fn_securitypredicate_result]
    WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS NVARCHAR(128))
       OR SESSION_CONTEXT(N'TenantId') IS NULL -- Allow administrative/unfiltered access if configured
);
GO

IF EXISTS (SELECT * FROM sys.security_policies WHERE name = N'AuditTenantSecurityPolicy')
BEGIN
    DROP SECURITY POLICY [audit].[AuditTenantSecurityPolicy];
END
GO

CREATE SECURITY POLICY [audit].[AuditTenantSecurityPolicy]
    ADD FILTER PREDICATE [audit].[fn_AuditTenantSecurityPredicate]([tenant_id]) ON [audit].[records],
    ADD BLOCK PREDICATE [audit].[fn_AuditTenantSecurityPredicate]([tenant_id]) ON [audit].[records] AFTER INSERT
    WITH (STATE = ON, SCHEMABINDING = ON);
GO
