# Troubleshooting Guide: EricksonLopez.Auditing

Diagnostic procedures and solutions for common runtime exceptions and configuration errors encountered when integrating `EricksonLopez.Auditing`.

---

## Common Issues and Solutions

### 1. `InvalidOperationException: No service for type 'IAuditStore' has been registered.`

* **Cause:** `services.AddAuditing()` was called, but no storage adapter was configured. By design, the library does not register a default in-memory store to prevent accidental data loss in production.
* **Resolution:** Configure your intended storage adapter or test double:
  ```csharp
  // Production:
  services.AddAuditing()
          .UsePostgreSql(opts => opts.ConnectionFactory = ...);

  // Testing:
  services.AddAuditing()
          .UseStore<InMemoryAuditStore>();
  ```

---

### 2. `InvalidOperationException: All records in a batch must belong to the same tenant.`

* **Cause:** `IAuditStore.AppendBatchAsync()` was called with a collection containing records from multiple different `TenantId` values.
* **Resolution:** Relational storage engines set session-level context (e.g. PostgreSQL `audit.tenant_id`) before issuing batch writes. Group records by tenant before calling `AppendBatchAsync`:
  ```csharp
  var batches = records.GroupBy(r => r.Context.TenantId);
  foreach (var batch in batches)
  {
      await auditStore.AppendBatchAsync(batch.ToList(), ct);
  }
  ```

---

### 3. `SqliteException: SQLite Error 1: 'no such table: audit_records'` (In-Memory Mode)

* **Cause:** SQLite in `:memory:` mode destroys database schema as soon as the connection that created it closes. If the connection factory creates and immediately disposes transient connections, the database re-initializes empty.
* **Resolution:** Use a shared cache connection string and keep a master connection open for the lifetime of the application or test run:
  ```csharp
  const string connStr = "Data Source=AuditMemoryDb;Mode=Memory;Cache=Shared";
  var masterConnection = new SqliteConnection(connStr);
  masterConnection.Open(); // Keep open

  services.AddAuditing()
          .UseSqlite(opts => opts.ConnectionFactory = () => new SqliteConnection(connStr));
  ```

---

### 4. `Chain break: previous_hash does not match predecessor's integrity_hash`

* **Cause:** HMAC chain verification detected that a record was deleted from the database or its `previous_hash` column was corrupted.
* **Resolution:** Inspect `result.FirstFailedRecordId` returned by `IAuditIntegrityVerifier.VerifyChainAsync()`. This identifies the exact record boundary where the chain was broken.

---

### 5. `Integrity hash mismatch: record content has been tampered with.`

* **Cause:** A key domain property (Actor, Action, Resource, Outcome, or Timestamp) of a persisted record was modified directly in the database after the original cryptographic signature was calculated.
* **Resolution:** A forensic tampering attempt has been detected. Cross-reference database access audit logs at the timestamp of `result.FirstFailedRecordId` to identify unauthorized modifications.

---

### 6. Native AOT Compilation Warnings (IL2026 / IL3050)

* **Cause:** Attempting to serialize custom change types using reflection-based JSON serializers instead of source-generated contexts.
* **Resolution:** The core library uses `AuditJsonContext` source generation. Avoid injecting unconstrained reflection serializers in custom adapter implementations.
