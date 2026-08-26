# Best Practices & Security Guidelines: EricksonLopez.Auditing

Engineering guidelines and security recommendations for maintaining forensic data integrity, compliance readiness, and maximum performance in mission-critical environments.

---

## 1. Forensic Security & Data Protection (GDPR / PCI-DSS / SOC2)

### 1.1 Zero-Leakage Invariant
* **Never Log Plain-Text Secrets:** Ensure passwords, API keys, JWTs, and credit card numbers are never placed in `AuditChange` values.
* **Leverage the Global Denylist:** If your domain uses custom secret property names (e.g. `ClientOAuthSecret`, `TaxpayerPin`), register them during initialization:
  ```csharp
  services.AddAuditing(cfg =>
  {
      cfg.GlobalFieldDenylist.Add("ClientOAuthSecret");
      cfg.GlobalFieldDenylist.Add("TaxpayerPin");
  });
  ```
* **Use `AuditChange.Redacted(fieldName)`:** When you need proof that a confidential field was modified without exposing historical or new values.
* **Use One-Way Hashing:** Use `AuditSensitivityPipeline.HashValue(plainText)` (SHA-256) when equality verification is required without storing raw data.

### 1.2 Structured `ErrorCode` Boundary
* `AuditRecord.ErrorCode` must strictly hold structured domain error identifiers (e.g., `AUTHZ_INSUFFICIENT_PERMISSIONS`, `VALIDATION_FAILED`).
* **Strictly Prohibited:** Storing raw exception messages (`ex.Message`), stack traces (`ex.StackTrace`), or database connection strings in `ErrorCode`.

---

## 2. Performance & Scalability

### 2.1 Keyset Pagination Over Traditional Offsets
* **Never use OFFSET on high-volume audit tables:** Offset queries scan and discard $N$ rows. In tables with millions of records, `OFFSET 50000` causes severe table scans.
* Use `AuditQuery.AfterRecordId` for $O(1)$ index seek pagination using the composite index `(tenant_id, occurred_at DESC, id DESC)`.

### 2.2 Batch Insertion with `AppendBatchAsync()`
* In queue workers, event stream consumers (Kafka/RabbitMQ), and batch ETL jobs, group records by `TenantId` and invoke `AppendBatchAsync()`.
* This reduces network round-trips from $N$ to $1$, yielding 10x-50x higher throughput.

### 2.3 Monotonic IDs with `AuditId.NewId()`
* Always generate identifiers with `AuditId.NewId()` instead of `Guid.NewGuid()`. `AuditId` generates RFC 9562 UUIDv7 IDs, which append sequentially to B-Tree indexes and prevent database index page splits.

---

## 3. Multi-Tenant Isolation & Database Security

### 3.1 Mandatory TenantId
* Never leave `TenantId` empty or populate it with random mock strings in production.
* For platform-wide background jobs and administrative maintenance, use the reserved constant `AuditContext.SystemTenantId` (`"system"`).

### 3.2 Row-Level Security in PostgreSQL
* Always apply the official database migration enabling `FORCE ROW LEVEL SECURITY`.
* `PostgreSqlAuditStore` automatically executes `SELECT set_config('audit.tenant_id', ...)` prior to running commands, guaranteeing that Tenant A can never read or write records belonging to Tenant B.

---

## 4. Cryptographic HMAC-SHA256 Integrity Chain

### 4.1 Secure Key Management
* Never store cryptographic HMAC keys in plain-text configuration files (`appsettings.json`) or unencrypted environment variables.
* Implement `IAuditIntegrityProvider` backed by a hardware security module or cloud KMS (Azure Key Vault, AWS KMS, HashiCorp Vault).

### 4.2 Automated Periodic Chain Verification
* Schedule automated background jobs (cron / scheduled worker) to execute `IAuditIntegrityVerifier.VerifyChainAsync()` daily or weekly, alerting security operations immediately upon any tampering detection.
