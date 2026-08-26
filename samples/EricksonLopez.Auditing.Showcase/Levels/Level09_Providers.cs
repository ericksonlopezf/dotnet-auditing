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
/// Nivel 9 — Adaptadores de Persistencia y Ecosistema de Observabilidad.
/// Demuestra SQLite, EF Core, Dapper, y OpenTelemetry de forma funcional.
///
/// NOTA: PostgreSQL, SQL Server, MySQL, Oracle y MongoDB requieren infraestructura
/// live externa y NO pueden ejecutarse en este Showcase sin conexión real.
/// Sus APIs de registro DI están documentadas al final de este nivel.
/// </summary>
public static class Level09_Providers
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 9] — ADAPTADORES DE PERSISTENCIA Y OBSERVABILIDAD (OPENTELEMETRY)");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        const string sqliteConnStr = "Data Source=ShowcaseSqliteDb;Mode=Memory;Cache=Shared";

        // ── 1. Adaptador SQLite Funcional en Memoria ──────────────────────────────────────
        Console.WriteLine("── 1. Almacén SQLite Funcional (EricksonLopez.Auditing.Sqlite) ──");
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
        Console.WriteLine($"✓ SQLite AppendAsync → QueryAsync: {sqliteQuery.Records.Count} registro(s).");
        Console.WriteLine($"  • SqliteAuditStoreOptions.Table: '{sqliteSp.GetRequiredService<SqliteAuditStoreOptions>().Table}'");
        Console.WriteLine($"  • SqliteAuditIntegrityVerifier: registrado automáticamente por UseSqlite().\n");

        // ── 2. SqliteAuditIntegrityVerifier — IAuditIntegrityVerifier en SQLite ───
        Console.WriteLine("── 2. SqliteAuditIntegrityVerifier (IAuditIntegrityVerifier) con HMAC ──");

        // Para verificar integridad, necesitamos registrar también el servicio HMAC y el key provider
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
            new TestAuditIntegrityProvider()); // Provider de test con clave fija

        var integritySp = sqliteIntegrityServices.BuildServiceProvider();
        var hmacService = integritySp.GetRequiredService<HmacAuditIntegrityService>();
        var verifier = integritySp.GetRequiredService<SqliteAuditIntegrityVerifier>();

        // IAuditIntegrityVerifier — interfaz de verificación de cadena (upcast para demostrar la interfaz)
        // La interfaz es el contrato de uso; SqliteAuditIntegrityVerifier es la implementación concreta.
        Console.WriteLine($"✓ SqliteAuditIntegrityVerifier implementa IAuditIntegrityVerifier: {verifier is IAuditIntegrityVerifier}");

        // Verificar la cadena en el rango de tiempo actual
        var verifyResult = await verifier.VerifyChainAsync(
            tenantId: "tenant-sqlite",
            from: DateTimeOffset.UtcNow.AddHours(-1),
            until: DateTimeOffset.UtcNow.AddMinutes(1));

        // Resultado esperado: IsValid=true, VerifiedCount=0 (registros sin hash de integridad)
        Console.WriteLine($"  • VerifyChainAsync resultado: IsValid={verifyResult.IsValid}, VerifiedCount={verifyResult.VerifiedCount}");
        Console.WriteLine($"  • AuditIntegrityVerificationResult: IsValid, VerifiedCount, FirstFailedRecordId?, FailureReason?\n");

        // ── 3. Adaptador Dapper Genérico Funcional ────────────────────────────────────
        Console.WriteLine("── 3. Almacén Dapper Genérico Funcional (EricksonLopez.Auditing.Dapper) ──");

        // DapperAuditStore es ANSI SQL genérico: compatible con SQL Server, PostgreSQL, MySQL, Oracle
        // NOTA: SQLite almacena TEXT los UUIDs — la proyección Guid→TEXT no es compatible con
        //       el deserializador genérico de Dapper. Para la demo, mostramos AppendAsync (INSERT).
        //       QueryAsync requiere un motor RDBMS que soporte tipos Guid/UUID nativamente.
        CreateDapperStoreSchema(masterConn, "audit_records_dapper");

        var dapperServices = new ServiceCollection();
        dapperServices.AddAuditing()
                      .UseDapper(options =>
                      {
                          // En producción: options.ConnectionFactory = () => new SqlConnection(connectionString);
                          // En esta demo usamos SQLite como backend de esquema compatible
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
        Console.WriteLine($"  • DapperAuditStore: ANSI SQL genérico — compatible con cualquier IDbConnection.");
        Console.WriteLine($"  • Interfaces del método de registro: AddAuditing().UseDapper(configure)\n");

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

        // AppendAsync funciona con SQLite (INSERT ANSI SQL)
        await dapperStore.AppendAsync(dapperRecord);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ DapperAuditStore.AppendAsync completado (ID: {dapperRecord.Id}).");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠ QueryAsync requiere motor RDBMS con tipo UUID nativo (SQL Server, PostgreSQL, MySQL, Oracle).");
        Console.WriteLine($"  ⚠ SQLite almacena UUIDs como TEXT — incompatible con el deserializador Guid de Dapper genérico.");
        Console.WriteLine($"  Para QueryAsync funcional, use SqliteAuditStore (adaptador específico SQLite) o un motor RDBMS.\n");
        Console.ResetColor();



        // ── 4. Entity Framework Core (⚠️ No completamente AOT-safe per ADR-001) ──
        Console.WriteLine("── 4. Almacén Entity Framework Core (⚠️ No AOT-compatible — ADR-001) ──");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   ⚠ AVISO: EricksonLopez.Auditing.EntityFrameworkCore NO es 100% AOT-safe.");
        Console.WriteLine("   ADR-001 documenta esta limitación. Use para aplicaciones que ya dependan de EF Core.");
        Console.WriteLine("   Para producción AOT-first, prefiera los adaptadores Dapper específicos por motor.");
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
        Console.WriteLine($"✓ EF Core AuditStore: {efQuery.Records.Count} registro(s) persistido(s).\n");

        // ── 5. Métodos de Registro DI para Motores de Producción ─────────────────
        Console.WriteLine("── 5. Métodos de Registro DI para Motores de Producción (Referencia) ──");
        Console.WriteLine("   Estos adaptadores requieren infraestructura real y no pueden ejecutarse aquí:");
        Console.WriteLine();
        Console.WriteLine("   // PostgreSQL (RLS + HMAC + Particionamiento mensual):");
        Console.WriteLine("   services.AddAuditing().UsePostgreSql(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new NpgsqlConnection(connectionString));");
        Console.WriteLine("   // Opciones: opts.Schema (default: \"audit\"), opts.Table (default: \"records\")");
        Console.WriteLine("   // Registra: PostgreSqlAuditStore, PostgreSqlAuditIntegrityVerifier");
        Console.WriteLine();
        Console.WriteLine("   // SQL Server / Azure SQL (SESSION_CONTEXT + SECURITY POLICY):");
        Console.WriteLine("   services.AddAuditing().UseSqlServer(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new SqlConnection(connectionString));");
        Console.WriteLine("   // Registra: SqlServerAuditStore, SqlServerAuditIntegrityVerifier");
        Console.WriteLine();
        Console.WriteLine("   // MySQL 8.0+ / MariaDB (Session context + InnoDB):");
        Console.WriteLine("   services.AddAuditing().UseMySql(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new MySqlConnection(connectionString));");
        Console.WriteLine("   // Registra: MySqlAuditStore, MySqlAuditIntegrityVerifier");
        Console.WriteLine();
        Console.WriteLine("   // Oracle 19c/21c/23ai (SYS_CONTEXT / VPD):");
        Console.WriteLine("   services.AddAuditing().UseOracle(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new OracleConnection(connectionString));");
        Console.WriteLine("   // Registra: OracleAuditStore, OracleAuditIntegrityVerifier");
        Console.WriteLine();
        Console.WriteLine("   // MongoDB (sin schema fijo, multi-tenant BSON):");
        Console.WriteLine("   services.AddMongoDbAuditStore(sp => sp.GetRequiredService<IMongoDatabase>(),");
        Console.WriteLine("       opts =>  // MongoAuditStoreOptions:");
        Console.WriteLine("       {");
        Console.WriteLine("           opts.CollectionName = \"audit_records\";  // default");
        Console.WriteLine("           opts.DatabaseName   = \"AuditingDb\";      // default");
        Console.WriteLine("       });");
        Console.WriteLine();
        Console.WriteLine("   // Dapper genérico (cualquier IDbConnection / ANSI SQL):");
        Console.WriteLine("   services.AddAuditing().UseDapper(opts => opts.ConnectionFactory =");
        Console.WriteLine("       () => new SqlConnection(connectionString));");
        Console.WriteLine("   // opts.Table: nombre de tabla custom (default: \"audit_records\")");
        Console.WriteLine();

        // ── 6. OpenTelemetry: ActivitySource, Métricas y EnrichCurrentActivity ───
        Console.WriteLine("── 6. OpenTelemetry Semántico (EricksonLopez.Auditing.OpenTelemetry) ──");
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

        // Demostrar EnrichCurrentActivity() con un Activity activo
        var activitySource = new ActivitySource("ShowcaseSource");
        using (var listener = new ActivityListener())
        {
            listener.ShouldListenTo = _ => true;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
            ActivitySource.AddActivityListener(listener);

            using var activity = activitySource.StartActivity("ShowcaseAuditOperation");
            if (activity is not null)
            {
                // Extension method en AuditRecord que enriquece el Activity.Current
                sqliteRecord.EnrichCurrentActivity();
                Console.WriteLine($"\n✓ AuditRecord.EnrichCurrentActivity() — Tags aplicados al Activity actual:");
                foreach (var tag in activity.Tags)
                {
                    Console.WriteLine($"    {tag.Key} = {tag.Value}");
                }
            }
        }

        // Métricas disponibles
        AuditMetrics.RecordsAppended.Add(1);
        AuditMetrics.QueriesExecuted.Add(1);
        AuditMetrics.IntegrityVerifications.Add(1);
        Console.WriteLine("\n✓ Contadores OpenTelemetry incrementados:");
        Console.WriteLine($"  • AuditMetrics.RecordsAppended (\"audit.records_appended\")");
        Console.WriteLine($"  • AuditMetrics.QueriesExecuted (\"audit.queries_executed\")");
        Console.WriteLine($"  • AuditMetrics.IntegrityVerifications (\"audit.integrity_verifications\")\n");
    }

    // Helper: crea el schema de tabla para SqliteAuditStore (usa columna 'changes')
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

    // Helper: crea el schema de tabla para DapperAuditStore (usa columna 'changes_json')
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
