// Copyright © Erickson Lopez. MIT License.
using System.Threading.Tasks;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;
using Xunit;

namespace EricksonLopez.Auditing.IntegrationTests;

public sealed class OracleFixture : IAsyncLifetime
{
    public OracleContainer Container { get; } = new OracleBuilder("gvenzl/oracle-xe:21-slim-faststart").Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        using var conn = new OracleConnection(Container.GetConnectionString());
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            CREATE TABLE AUDIT_RECORDS (
                id VARCHAR2(36) PRIMARY KEY,
                occurred_at TIMESTAMP(6) WITH TIME ZONE NOT NULL,
                tenant_id VARCHAR2(100) NOT NULL,
                source VARCHAR2(100) NOT NULL,
                actor_type VARCHAR2(50),
                actor_id VARCHAR2(100),
                actor_name VARCHAR2(100),
                action_code VARCHAR2(100) NOT NULL,
                resource_type VARCHAR2(100) NOT NULL,
                resource_id VARCHAR2(100) NOT NULL,
                aggregate_type VARCHAR2(100),
                aggregate_id VARCHAR2(100),
                outcome NUMBER(5) NOT NULL,
                error_code VARCHAR2(100),
                correlation_id VARCHAR2(100),
                causation_id VARCHAR2(100),
                request_id VARCHAR2(100),
                ip_address VARCHAR2(45),
                user_agent VARCHAR2(1000),
                idempotency_key VARCHAR2(128),
                changes CLOB,
                integrity_hash VARCHAR2(100),
                previous_hash VARCHAR2(100)
            )");
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
