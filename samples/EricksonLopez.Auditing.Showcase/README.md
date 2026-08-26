# EricksonLopez.Auditing.Showcase

Official reference implementation and executable demonstration suite for the **`EricksonLopez.Auditing`** ecosystem.

---

## 🎯 Purpose of the Showcase

The `Showcase` project serves as the **executable documentation** of the public API surface. It guarantees that:

* Every public API is backed by an executable reference implementation.
* No fictitious, deprecated, or obsolete APIs exist in documentation.
* Provides a progressive pedagogical learning path from Level 0 (Conceptual) to Level 10 (Enterprise Architecture).
* Functions as a testbed and living cookbook of official integration patterns.

---

## 🚀 Running the Showcase

### Interactive Mode
```bash
dotnet run --project samples/EricksonLopez.Auditing.Showcase/EricksonLopez.Auditing.Showcase.csproj --framework net9.0
```

### Batch Mode (Run All 11 Levels)
```bash
dotnet run --project samples/EricksonLopez.Auditing.Showcase/EricksonLopez.Auditing.Showcase.csproj --framework net9.0 -- --all
```

---

## 📚 Pedagogical Levels

| Level | Title | Demonstrated Concepts & APIs |
| :--- | :--- | :--- |
| **Level 0** | Conceptual | Philosophy, auditing vs logging/tracing, append-only invariants |
| **Level 1** | Quick Start | `AuditRecord`, `AuditId.NewId()`, `AuditActor`, `AuditAction`, `AuditResource`, `AuditContext` (with `CausationId`, `RequestId`, `IpAddress`, `UserAgent`), `AuditContext.SystemTenantId`, `AuditOutcome`, `AuditChange`, `IAuditStore.AppendAsync()`, `QueryAsync()` with `ActorId`/`From`/`To`/`CorrelationId` |
| **Level 2** | Full Configuration | `AuditConfiguration`, `AuditFailureBehavior`, `AuditFieldSensitivity` (Include/Exclude/Redact/Hash), `AuditSensitivityPipeline`, `GlobalFieldDenylist`, `CriticalActionCodes`, asynchronous batching |
| **Level 3** | Real-World Use Cases | `AuditAction.Login`, `GrantPermission`, `Update`, `Download`, `Send`, `Cancel`, `Restore`, `AuditOutcome.Denied`, custom `AuditAction`, `AuditResource.AggregateType/AggregateId`, `AuditChange.Redacted()`, `AuditContext.SystemTenantId` |
| **Level 4** | Advanced Integration | `AuditScope.Begin()`, `AuditScope.Current`, `WithMetadata()`, nested ambient scope hierarchy restoration |
| **Level 5** | Batch Processing | `IAuditStore.AppendBatchAsync()`, `InMemoryAuditStore.ForTenant()`, `ForActor()`, `Count`, `Clear()`, multi-tenant batch validation |
| **Level 6** | Error Handling | `AuditFailureBehavior.FailClosed/FailOpen/Deferred`, `AuditOutcome.Failure/Denied/Cancelled/Partial`, structured `ErrorCode` boundaries |
| **Level 7** | Scalability | Keyset cursor pagination with `AuditQuery.AfterRecordId`, `AuditQuery.ActorId`, `CorrelationId`, `From/To`, `Outcome`, `ActionCode`, `ResourceType` |
| **Level 8** | Customization | `IAuditActorProvider`, `IAuditContextProvider`, `IAuditIntegrityProvider`, `SystemAuditActorProvider.Instance`, custom `IAuditStore`, `IAuditBuilder.UseActorProvider/EnableIntegrityChain/UseStore`, `HmacAuditIntegrityService.ComputeHash/Verify` |
| **Level 9** | Storage Providers & OpenTelemetry | `SqliteAuditStore`/`SqliteAuditStoreOptions`/`UseSqlite()`, `SqliteAuditIntegrityVerifier` (`IAuditIntegrityVerifier`), `DapperAuditStore`/`DapperAuditStoreOptions`/`UseDapper()`, `AddEntityFrameworkCoreAuditStore()`, `AuditActivitySource`, `AuditMetrics`, `EnrichCurrentActivity()`, `AddMongoDbAuditStore()`, PostgreSQL/SqlServer/MySQL/Oracle integrations |
| **Level 10** | Enterprise Architecture | `AuditSensitivityPipeline.Apply()`, `HashValue()`, `AuditRecordBuilder.Create()`, `BuildDefault()`, `WithXxx()` fluent chain, `AddChange()`, `AddRedactedChange()`, `Build()`, `TestAuditIntegrityProvider.SetTenantKey()`, `DefaultKey`, HMAC cryptographic chain, tampering detection, `IAuditIntegrityVerifier` |

---

## 🗃️ Public API Coverage Matrix

| Package | Type / Interface | Showcase Status |
| :--- | :--- | :---: |
| **Abstractions** | `IAuditStore` | ✅ Verified |
| **Abstractions** | `IAuditActorProvider` | ✅ Verified |
| **Abstractions** | `IAuditContextProvider` | ✅ Verified |
| **Abstractions** | `IAuditIntegrityProvider` | ✅ Verified |
| **Abstractions** | `IAuditIntegrityVerifier` | ✅ Verified |
| **Abstractions** | `IAuditBuilder` | ✅ Verified |
| **Abstractions** | `AuditRecord` | ✅ Verified |
| **Abstractions** | `AuditActor` / `AuditActorType` | ✅ Verified |
| **Abstractions** | `AuditAction` (all predefined actions) | ✅ Verified |
| **Abstractions** | `AuditResource` (with Aggregate properties) | ✅ Verified |
| **Abstractions** | `AuditContext` (all properties & `SystemTenantId`) | ✅ Verified |
| **Abstractions** | `AuditChange` / `Redacted()` | ✅ Verified |
| **Abstractions** | `AuditQuery` (all filter parameters) | ✅ Verified |
| **Abstractions** | `AuditQueryResult` | ✅ Verified |
| **Abstractions** | `AuditIntegrityVerificationResult` | ✅ Verified |
| **Abstractions** | `HmacAuditIntegrityService` | ✅ Verified |
| **Abstractions** | `SystemAuditActorProvider.Instance` | ✅ Verified |
| **Core** | `AuditId.NewId()` | ✅ Verified |
| **Core** | `AuditScope` | ✅ Verified |
| **Core** | `AuditConfiguration` | ✅ Verified |
| **Core** | `AuditFailureBehavior` | ✅ Verified |
| **Core** | `AuditFieldSensitivity` | ✅ Verified |
| **Core** | `AuditSensitivityPipeline` | ✅ Verified |
| **Core** | `AddAuditing()` | ✅ Verified |
| **Testing** | `InMemoryAuditStore` (incl. `ForTenant/ForActor/Clear/Count`) | ✅ Verified |
| **Testing** | `AuditRecordBuilder` (fluent chain) | ✅ Verified |
| **Testing** | `TestAuditIntegrityProvider` (incl. `SetTenantKey`) | ✅ Verified |
| **PostgreSql** | `UsePostgreSql()` / `PostgreSqlAuditIntegrityVerifier` | ✅ Reference |
| **SqlServer** | `UseSqlServer()` / `SqlServerAuditIntegrityVerifier` | ✅ Reference |
| **Sqlite** | `UseSqlite()` / `SqliteAuditIntegrityVerifier` | ✅ Functional |
| **MySql** | `UseMySql()` / `MySqlAuditIntegrityVerifier` | ✅ Reference |
| **Oracle** | `UseOracle()` / `OracleAuditIntegrityVerifier` | ✅ Reference |
| **MongoDb** | `AddMongoDbAuditStore()` / `MongoAuditStoreOptions` | ✅ Reference |
| **Dapper** | `UseDapper()` / `DapperAuditStore` / `DapperAuditStoreOptions` | ✅ Functional |
| **EntityFrameworkCore** | `AddEntityFrameworkCoreAuditStore()` | ✅ Functional |
| **OpenTelemetry** | `AuditActivitySource` / `AuditMetrics` / `EnrichCurrentActivity()` | ✅ Functional |
