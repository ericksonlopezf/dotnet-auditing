// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Dapper;
using EricksonLopez.Auditing.EntityFrameworkCore;
using EricksonLopez.Auditing.OpenTelemetry;
using EricksonLopez.Auditing.Sqlite;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 9 — Persistence Adapters & Observability Ecosystem.
/// Demonstrates functional SQLite, EF Core, Dapper, and OpenTelemetry.
///
/// NOTE: PostgreSQL, SQL Server, MySQL, Oracle, and MongoDB require live external infrastructure
/// and cannot execute in this showcase without active connections.
/// Their DI registration APIs are documented as references at the end of this level.
/// </summary>
public static class Level09_Providers
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 9] — PERSISTENCE ADAPTERS & OBSERVABILITY (OPENTELEMETRY)");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        const string sqliteConnStr = "Data Source=ShowcaseSqliteDb;Mode=Memory;Cache=Shared";

        // ── 1. Functional In-Memory SQLite Adapter ────────────────────────────────
        Console.WriteLine("── 1. Functional SQLite Store (EricksonLopez.Auditing.Sqlite) ──");
        using var masterConn = new SqliteConnection(sqliteConnStr);
        masterConn.Open();

        CreateSqliteStoreSchema(masterConn, "audit_records_sqlite");

        var sqliteServices = new ServiceCollection();
        sqliteServices.AddAuditing()
                      .UseSqlite(options =>
                      {
                          options.ConnectionFactory = () =>
                          {
                              var conn = new SqliteConnection(sqliteConnStr);
                              conn.Open();
                              return conn;
                          };
                          options.Table = "audit_records_sqlite";
                      });

        var sqliteSp = sqliteServices.BuildServiceProvider();
        var sqliteStore = sqliteSp.GetRequiredService<IAuditStore>();

        var sqliteRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-sqlite-01", "SQLite Admin"),
            Action = AuditAction.Create,
            Resource = new AuditResource("LocalConfig", "cfg-001"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-sqlite", "LocalService")
        };

        await sqliteStore.AppendAsync(sqliteRecord);
        var sqliteQuery = await sqliteStore.QueryAsync(new AuditQuery { TenantId = "tenant-sqlite" });
        Console.WriteLine($"✓ SQLite AppendAsync → QueryAsync: {sqliteQuery.Records.Count} record(s).");
        Console.WriteLine($"  • SqliteAuditStoreOptions.Table: '{sqliteSp.GetRequiredService<SqliteAuditStoreOptions>().Table}'");
        Console.WriteLine($"  • SqliteAuditIntegrityVerifier: Registered automatically by UseSqlite().\n");

        // ── 2. SqliteAuditIntegrityVerifier — IAuditIntegrityVerifier on SQLite ───
        Console.WriteLine("── 2. SqliteAuditIntegrityVerifier (IAuditIntegrityVerifier) with HMAC ──");

        var sqliteIntegrityServices = new ServiceCollection();
        sqliteIntegrityServices.AddAuditing()
                               .EnableIntegrityChain()
                               .UseSqlite(options =>
                               {
                                   options.ConnectionFactory = () =>
                                   {
                                       var conn = new SqliteConnection(sqliteConnStr);
                                       conn.Open();
                                       return conn;
                                   };
                                   options.Table = "audit_records_sqlite";
                               });
        sqliteIntegrityServices.AddSingleton<IAuditIntegrityProvider>(
            new TestAuditIntegrityProvider());

        var integritySp = sqliteIntegrityServices.BuildServiceProvider();
        var hmacService = integritySp.GetRequiredService<HmacAuditIntegrityService>();
        var verifier = integritySp.GetRequiredService<SqliteAuditIntegrityVerifier>();

        Console.WriteLine($"✓ SqliteAuditIntegrityVerifier implements IAuditIntegrityVerifier: {verifier is IAuditIntegrityVerifier}");

        var verifyResult = await verifier.VerifyChainAsync(
            tenantId: "tenant-sqlite",
            from: DateTimeOffset.UtcNow.AddHours(-1),
            until: DateTimeOffset.UtcNow.AddMinutes(1));

        Console.WriteLine($"  • VerifyChainAsync result: IsValid={verifyResult.IsValid}, VerifiedCount={verifyResult.VerifiedCount}");
        Console.WriteLine($"  • AuditIntegrityVerificationResult: IsValid, VerifiedCount, FirstFailedRecordId?, FailureReason?\n");

        // ── 3. Functional Generic Dapper Adapter ──────────────────────────────────
        Console.WriteLine("── 3. Functional Generic Dapper Store (EricksonLopez.Auditing.Dapper) ──");

        CreateDapperStoreSchema(masterConn, "audit_records_dapper");

        var dapperServices = new ServiceCollection();
        dapperServices.AddAuditing()
                      .UseDapper(options =>
                      {
                          options.ConnectionFactory = () =>
                          {
                              var conn = new SqliteConnection(sqliteConnStr);
                              conn.Open();
                              return conn;
                          };
                          options.Table = "audit_records_dapper";
                      });

        var dapperSp = dapperServices.BuildServiceProvider();
        var dapperStore = dapperSp.GetRequiredService<IAuditStore>();
        var dapperOptions = dapperSp.GetRequiredService<DapperAuditStoreOptions>();

        Console.WriteLine($"  • DapperAuditStoreOptions.Table: '{dapperOptions.Table}'");
        Console.WriteLine($"  • DapperAuditStore: Generic ANSI SQL — compatible with any IDbConnection.");
        Console.WriteLine($"  • Registration method: AddAuditing().UseDapper(configure)\n");

        var dapperRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.Service, "data-migration-svc", "Data Migration Service"),
            Action = AuditAction.Create,
            Resource = new AuditResource("MigrationJob", "job-v2-upgrade"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-dapper",
                Source: "MigrationWorker",
                CorrelationId: "corr-migration-job-01")
        };

        await dapperStore.AppendAsync(dapperRecord);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ DapperAuditStore.AppendAsync completed (ID: {dapperRecord.Id}).");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠ QueryAsync requires an RDBMS with native UUID types (SQL Server, PostgreSQL, MySQL, Oracle).");
        Console.WriteLine($"  ⚠ SQLite stores UUIDs as TEXT — incompatible with generic Dapper Guid deserializer.");
        Console.WriteLine($"  For SQLite QueryAsync, use SqliteAuditStore (dedicated SQLite provider).\n");
        Console.ResetColor();

        // ── 4. Entity Framework Core Store ───────────────────────────────────────
        Console.WriteLine("── 4. Entity Framework Core Store (⚠️ Non-AOT per ADR-0001/0005) ──");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   ⚠ NOTICE: EricksonLopez.Auditing.EntityFrameworkCore is not 100% Native AOT-safe.");
        Console.WriteLine("   Use for standard ASP.NET Core apps already using EF Core.");
        Console.WriteLine("   For Native AOT production, prefer dedicated Dapper engine adapters.");
        Console.ResetColor();

        var efServices = new ServiceCollection();
        efServices.AddEntityFrameworkCoreAuditStore(builder =>
        {
            builder.UseInMemoryDatabase("ShowcaseAuditDb");
        });

        var efSp = efServices.BuildServiceProvider();
        var efStore = efSp.GetRequiredService<IAuditStore>();

        var efRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.SystemProcess, "system"),
            Action = AuditAction.Restore,
            Resource = new AuditResource("BackupSnapshot", "snap-2026"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-ef", "BackupWorker")
        };

        await efStore.AppendAsync(efRecord);
        var efQuery = await efStore.QueryAsync(new AuditQuery { TenantId = "tenant-ef" });
        Console.WriteLine($"✓ EF Core AuditStore: {efQuery.Records.Count} record(s) persisted.\n");

        // ── 5. Production Engine DI Registration Reference ───────────────────────
        Console.WriteLine("── 5. Production Storage Engine DI Registration (Reference) ──");
        Console.WriteLine("   These adapters require live database engines:");
        Console.WriteLine();
        Console.WriteLine("   // PostgreSQL (RLS + HMAC + Monthly Partitioning):");
        Console.WriteLine("   services.AddAuditing().UsePostgreSql(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new NpgsqlConnection(connectionString));");
        Console.WriteLine();
        Console.WriteLine("   // SQL Server / Azure SQL (SESSION_CONTEXT + SECURITY POLICY):");
        Console.WriteLine("   services.AddAuditing().UseSqlServer(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new SqlConnection(connectionString));");
        Console.WriteLine();
        Console.WriteLine("   // MySQL 8.0+ / MariaDB (Session context + InnoDB):");
        Console.WriteLine("   services.AddAuditing().UseMySql(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new MySqlConnection(connectionString));");
        Console.WriteLine();
        Console.WriteLine("   // Oracle 19c/21c/23ai (SYS_CONTEXT / VPD):");
        Console.WriteLine("   services.AddAuditing().UseOracle(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new OracleConnection(connectionString));");
        Console.WriteLine();
        Console.WriteLine("   // MongoDB (Multi-tenant BSON Document Store):");
        Console.WriteLine("   services.AddMongoDbAuditStore(sp => sp.GetRequiredService<IMongoDatabase>(),");
        Console.WriteLine("       opts => { opts.CollectionName = \"audit_records\"; opts.DatabaseName = \"AuditingDb\"; });");
        Console.WriteLine();

        // ── 6. Semantic OpenTelemetry: ActivitySource & Metrics ──────────────────
        Console.WriteLine("── 6. Semantic OpenTelemetry (EricksonLopez.Auditing.OpenTelemetry) ──");
        Console.WriteLine($"   • ActivitySource.Name: '{AuditActivitySource.ActivitySourceName}'");
        Console.WriteLine($"   • Meter.Name: '{AuditMetrics.MeterName}'");
        Console.WriteLine($"   • Semantic Tags:");
        Console.WriteLine($"     - {AuditActivitySource.Tags.TenantId}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.ActionCode}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.ResourceType}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.ResourceId}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.ActorId}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.ActorType}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.Outcome}");
        Console.WriteLine($"     - {AuditActivitySource.Tags.RecordId}");

        var activitySource = new ActivitySource("ShowcaseSource");
        using (var listener = new ActivityListener())
        {
            listener.ShouldListenTo = _ => true;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
            ActivitySource.AddActivityListener(listener);

            using var activity = activitySource.StartActivity("ShowcaseAuditOperation");
            if (activity is not null)
            {
                sqliteRecord.EnrichCurrentActivity();
                Console.WriteLine($"\n✓ AuditRecord.EnrichCurrentActivity() — Tags applied to current Activity:");
                foreach (var tag in activity.Tags)
                {
                    Console.WriteLine($"    {tag.Key} = {tag.Value}");
                }
            }
        }

        AuditMetrics.RecordsAppended.Add(1);
        AuditMetrics.QueriesExecuted.Add(1);
        AuditMetrics.IntegrityVerifications.Add(1);
        Console.WriteLine("\n✓ OpenTelemetry Counters Incremented:");
        Console.WriteLine($"  • AuditMetrics.RecordsAppended (\"audit.records_appended\")");
        Console.WriteLine($"  • AuditMetrics.QueriesExecuted (\"audit.queries_executed\")");
        Console.WriteLine($"  • AuditMetrics.IntegrityVerifications (\"audit.integrity_verifications\")\n");
    }

    private static void CreateSqliteStoreSchema(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {tableName} (
                id TEXT PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                tenant_id TEXT NOT NULL,
                source TEXT NOT NULL,
                actor_type INTEGER NOT NULL,
                actor_id TEXT NOT NULL,
                actor_name TEXT,
                action_code TEXT NOT NULL,
                resource_type TEXT NOT NULL,
                resource_id TEXT NOT NULL,
                aggregate_type TEXT,
                aggregate_id TEXT,
                outcome INTEGER NOT NULL,
                error_code TEXT,
                correlation_id TEXT,
                causation_id TEXT,
                request_id TEXT,
                ip_address TEXT,
                user_agent TEXT,
                changes TEXT,
                integrity_hash TEXT,
                previous_hash TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateDapperStoreSchema(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {tableName} (
                id TEXT PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                tenant_id TEXT NOT NULL,
                source TEXT NOT NULL,
                actor_type INTEGER NOT NULL,
                actor_id TEXT NOT NULL,
                actor_name TEXT,
                action_code TEXT NOT NULL,
                resource_type TEXT NOT NULL,
                resource_id TEXT NOT NULL,
                aggregate_type TEXT,
                aggregate_id TEXT,
                outcome INTEGER NOT NULL,
                error_code TEXT,
                correlation_id TEXT,
                causation_id TEXT,
                request_id TEXT,
                ip_address TEXT,
                user_agent TEXT,
                changes_json TEXT,
                integrity_hash TEXT,
                previous_hash TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }
}
