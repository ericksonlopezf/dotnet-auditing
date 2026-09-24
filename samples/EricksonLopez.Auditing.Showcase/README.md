<!-- Copyright © Erickson Lopez. MIT License. -->
# EricksonLopez.Auditing.Showcase

Official reference implementation and executable demonstration suite for the **`EricksonLopez.Auditing`** ecosystem.

---

## 🎯 Purpose of the Showcase

The `Showcase` project serves as the **executable documentation** of the public API surface. It guarantees that:

* Every public API is backed by an executable reference implementation.
* No fictitious, deprecated, or obsolete APIs exist in documentation.
* Provides a progressive pedagogical learning path from Level 0 (Conceptual) to Level 11 (Comprehensive 100% Verification Gate).
* Functions as a testbed and living cookbook of official integration patterns.

---

## 🚀 Running the Showcase

### Interactive Mode
```bash
dotnet run --project samples/EricksonLopez.Auditing.Showcase/EricksonLopez.Auditing.Showcase.csproj --framework net8.0
```

### Batch Mode (Run All 12 Levels)
```bash
dotnet run --project samples/EricksonLopez.Auditing.Showcase/EricksonLopez.Auditing.Showcase.csproj --framework net8.0 -- --all
```

*(Also supports `--framework net9.0` and `--framework net10.0`)*

---

## 📚 Pedagogical Levels

| Level | Title | Demonstrated Concepts & APIs |
| :--- | :--- | :--- |
| **Level 0** | Conceptual | Philosophy, auditing vs logging/tracing, append-only invariants, architectural boundaries |
| **Level 1** | Quick Start | `AuditRecord`, `AuditId.NewId()`, `AuditActor`, `AuditAction`, `AuditResource`, `AuditContext` (with `CausationId`, `RequestId`, `IpAddress`, `UserAgent`), `AuditContext.SystemTenantId`, `AuditOutcome`, `AuditChange`, `IAuditStore.AppendAsync()`, `QueryAsync()` with filters |
| **Level 2** | Full Configuration | `AuditConfiguration`, `AuditFailureBehavior`, `AuditFieldSensitivity` (Include/Exclude/Redact/Hash), `AuditSensitivityPipeline`, `GlobalFieldDenylist`, `CriticalActionCodes`, `MaxStringLength` |
| **Level 3** | Real-World Use Cases | `AuditAction.Login`, `GrantPermission`, `Update`, `Download`, `Send`, `Cancel`, `Restore`, `AuditOutcome.Denied`, custom `AuditAction`, `AuditResource.AggregateType/AggregateId`, `AuditChange.Redacted()`, `AuditContext.SystemTenantId` |
| **Level 4** | Advanced Integration | `AuditScope.Begin()`, `AuditScope.Current`, `WithMetadata()`, nested ambient scope hierarchy restoration with `AsyncLocal<T>` |
| **Level 5** | Batch Processing & Outbox | `IAuditStore.AppendBatchAsync()`, `InMemoryAuditStore.ForTenant()`, `ForActor()`, `Clear()`, multi-tenant batch safety invariant, `BufferedAuditStoreDecorator` (channel drain worker), `OutboxAuditStore` & `IOutboxMessageService` (transactional outbox) |
| **Level 6** | Error Handling & Resiliency | `AuditFailureBehavior.FailClosed/FailOpen/Deferred`, `ResilientAuditStoreDecorator` (active interception, swallowing in FailOpen, critical action rethrow), structured `ErrorCode` security boundaries |
| **Level 7** | Scalability | Keyset cursor pagination with `AuditCursorToken`, `AuditQuery.ContinuationToken`, `AuditQuery.ActorId`, `CorrelationId`, `From/To`, `Outcome`, `ActionCode`, `ResourceType` |
| **Level 8** | Customization & Extensibility | `IAuditActorProvider`, `IAuditContextProvider`, `IAuditIntegrityProvider`, `SystemAuditActorProvider.Instance`, custom `IAuditStore` (SIEM forwarder), `IAuditBuilder.UseActorProvider/EnableIntegrityChain/UseStore`, `HmacAuditIntegrityService.ComputeHash/Verify` |
| **Level 9** | Storage Providers & OpenTelemetry | `SqliteAuditStore`/`SqliteAuditStoreOptions`/`UseSqlite()`, `SqliteAuditIntegrityVerifier` (`IAuditIntegrityVerifier`), `DapperAuditStore`/`DapperAuditStoreOptions`/`UseDapper()`, `AddEntityFrameworkCoreAuditStore()`, `AuditActivitySource`, `AuditMetrics`, `EnrichCurrentActivity()`, `AddMongoDbAuditStore()`, PostgreSQL/SqlServer/MySQL/Oracle integrations |
| **Level 10** | Enterprise Architecture | `AuditSensitivityPipeline.ApplyAsync()`, `HashValue()`, `AuditRecordBuilder` fluent builder, `TestAuditIntegrityProvider.SetTenantKey()`, HMAC cryptographic chain, forensic tamper detection, GDPR Art. 17 Crypto-Shredding (`IAuditCryptoKeyProvider`), Azure Key Vault integration (`AzureKeyVaultIntegrityProvider`) |
| **Level 11** | Comprehensive API Coverage Gate | Keyset token parsing (`AuditCursorToken.TryParse`), IP anonymization (`AuditContext.AnonymizeIp`), ambient time provider (`AmbientAuditTimeProvider`), `AuditRecordBuilder` fluent API, `IAuditSensitivityPipeline.SanitizeAsync`, `IAuditLogger.LogAsync`, `DapperAuditStore.GetByIdAsync`, EF Core `ApplyAuditRecordConfiguration`, ecosystem DI registration checks (`UseSqlServer`, `UsePostgreSql`, `UseMySql`, `UseOracle`, `AddMongoDbAuditStore`), `TenantId` struct conversions, `AuditId.NewId`, `AuditFieldSensitivity` enum, Outbox batch verification, OpenTelemetry decorators (`OpenTelemetryAuditStoreDecorator`, `OpenTelemetryAuditIntegrityVerifierDecorator`), `IAuditHashAlgorithm` SPI |

---

## 🗃️ Public API Coverage Matrix

| Package | Type / Interface | Showcase Status |
| :--- | :--- | :---: |
| **Abstractions** | `IAuditStore` | ✅ Verified Executable |
| **Abstractions** | `IAuditActorProvider` | ✅ Verified Executable |
| **Abstractions** | `IAuditContextProvider` | ✅ Verified Executable |
| **Abstractions** | `IAuditIntegrityProvider` | ✅ Verified Executable |
| **Abstractions** | `IAuditIntegrityVerifier` | ✅ Verified Executable |
| **Abstractions** | `IAuditBuilder` | ✅ Verified Executable |
| **Abstractions** | `IAuditCryptoKeyProvider` | ✅ Verified Executable |
| **Abstractions** | `IAuditHashAlgorithm` | ✅ Verified Executable |
| **Abstractions** | `IAuditLogger` / `IAuditLogger<T>` | ✅ Verified Executable |
| **Abstractions** | `IAuditSensitivityPipeline` | ✅ Verified Executable |
| **Abstractions** | `IAuditTimeProvider` / `AmbientAuditTimeProvider` | ✅ Verified Executable |
| **Abstractions** | `AuditRecord` | ✅ Verified Executable |
| **Abstractions** | `AuditActor` / `AuditActorType` | ✅ Verified Executable |
| **Abstractions** | `AuditAction` (all predefined actions & custom codes) | ✅ Verified Executable |
| **Abstractions** | `AuditResource` (with Aggregate properties) | ✅ Verified Executable |
| **Abstractions** | `AuditContext` (all properties, `SystemTenantId`, `AnonymizeIp`) | ✅ Verified Executable |
| **Abstractions** | `AuditChange` / `Redacted()` | ✅ Verified Executable |
| **Abstractions** | `AuditCursorToken` (`Create`, `TryParse`) | ✅ Verified Executable |
| **Abstractions** | `TenantId` (value object, implicit conversions) | ✅ Verified Executable |
| **Abstractions** | `AuditQuery` (all filter parameters & keyset cursor) | ✅ Verified Executable |
| **Abstractions** | `AuditQueryResult` | ✅ Verified Executable |
| **Abstractions** | `AuditIntegrityVerificationResult` | ✅ Verified Executable |
| **Abstractions** | `HmacAuditIntegrityService` | ✅ Verified Executable |
| **Abstractions** | `HmacSha256AuditHashAlgorithm` | ✅ Verified Executable |
| **Abstractions** | `SystemAuditActorProvider.Instance` | ✅ Verified Executable |
| **Core** | `AuditId.NewId()` (UUIDv7) | ✅ Verified Executable |
| **Core** | `AuditScope` (`Begin`, `Current`, `WithMetadata`) | ✅ Verified Executable |
| **Core** | `AuditConfiguration` | ✅ Verified Executable |
| **Core** | `AuditFailureBehavior` (FailClosed, FailOpen, Deferred) | ✅ Verified Executable |
| **Core** | `AuditFieldSensitivity` (Include, Exclude, Redact, Hash) | ✅ Verified Executable |
| **Core** | `AuditSensitivityPipeline` (`SanitizeAsync`, `ApplyAsync`, `HashValue`) | ✅ Verified Executable |
| **Core** | `AuditRecordBuilder` | ✅ Verified Executable |
| **Core** | `AuditLogger<T>` | ✅ Verified Executable |
| **Core** | `BufferedAuditStoreDecorator` / `BufferedAuditStoreOptions` | ✅ Verified Executable |
| **Core** | `ResilientAuditStoreDecorator` | ✅ Verified Executable |
| **Core** | `IntegrityAuditStoreDecorator` | ✅ Verified Executable |
| **Core** | `AddAuditing()` / `EnableBuffering()` | ✅ Verified Executable |
| **AzureKeyVault** | `AzureKeyVaultIntegrityProvider` | ✅ Verified Executable |
| **Outbox** | `OutboxAuditStore` / `IOutboxMessageService` | ✅ Verified Executable |
| **Sqlite** | `UseSqlite()` / `SqliteAuditStore` / `SqliteAuditIntegrityVerifier` | ✅ Verified Executable |
| **Dapper** | `UseDapper()` / `DapperAuditStore` / `DapperAuditStoreOptions` | ✅ Verified Executable |
| **EntityFrameworkCore** | `AddEntityFrameworkCoreAuditStore()` / `AuditDbContext` / `ApplyAuditRecordConfiguration` | ✅ Verified Executable |
| **OpenTelemetry** | `AuditActivitySource` / `AuditMetrics` / `EnrichCurrentActivity()` | ✅ Verified Executable |
| **OpenTelemetry** | `OpenTelemetryAuditStoreDecorator` / `OpenTelemetryAuditIntegrityVerifierDecorator` | ✅ Verified Executable |
| **PostgreSql** | `UsePostgreSql()` / `PostgreSqlAuditIntegrityVerifier` / `PostgreSqlAuditStore` | ✅ Reference Validated |
| **SqlServer** | `UseSqlServer()` / `SqlServerAuditIntegrityVerifier` / `SqlServerAuditStore` | ✅ Reference Validated |
| **MySql** | `UseMySql()` / `MySqlAuditIntegrityVerifier` / `MySqlAuditStore` | ✅ Reference Validated |
| **Oracle** | `UseOracle()` / `OracleAuditIntegrityVerifier` / `OracleAuditStore` | ✅ Reference Validated |
| **MongoDb** | `AddMongoDbAuditStore()` / `MongoAuditStore` / `MongoAuditStoreOptions` | ✅ Reference Validated |
| **Testing** | `InMemoryAuditStore` (incl. `ForTenant/ForActor/Clear/Count`) | ✅ Verified Executable |
| **Testing** | `TestAuditIntegrityProvider` (incl. `SetTenantKey`) | ✅ Verified Executable |
