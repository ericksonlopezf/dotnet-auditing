# Anti-Patterns in Audit Logging & Persistence

This document identifies anti-patterns in audit logging, security compliance, and persistence design, explaining why they are prohibited in `EricksonLopez.Auditing` and how to resolve them.

---

## 1. Mutable Audit Records

### ❌ Anti-Pattern
Allowing audit log entries to be updated or deleted via API endpoints or ORM tracking.

```csharp
// BAD: Updating an existing audit record violates non-repudiation
var record = await dbContext.AuditRecords.FindAsync(auditId);
record.Status = "Archived";
await dbContext.SaveChangesAsync();
```

### ✅ Correct Pattern
Audit logs are strictly **append-only and immutable**. Any status change or compensating action must be recorded as a new, distinct audit event with its own monotonic UUIDv7 identifier and hash chain link.

```csharp
// GOOD: Record a new compensatory audit event
await auditScope.RecordAsync(new AuditEvent(
    action: "Order.StatusArchived",
    entityId: orderId,
    metadata: new { PriorAuditId = priorAuditId }));
```

---

## 2. Unredacted Sensitive Data (PII / Secrets Leakage)

### ❌ Anti-Pattern
Serializing raw user requests, passwords, credit card numbers, or authorization tokens directly into audit payloads.

```csharp
// BAD: Raw serialization leaks cleartext passwords/tokens
var payload = JsonSerializer.Serialize(userLoginRequest);
await auditScope.RecordAsync("User.Login", payload);
```

### ✅ Correct Pattern
Always process payloads through the `IAuditSensitivityPipeline` or annotate models with `[SensitiveData]` / `[Redact]` attributes.

```csharp
// GOOD: Redaction pipeline automatically masks PII before persistence
var sanitizedPayload = await sensitivityPipeline.ProcessAsync(userLoginRequest);
await auditScope.RecordAsync("User.Login", sanitizedPayload);
```

---

## 3. Out-of-Band Unchained Writes (Bypassing HMAC Hash Chain)

### ❌ Anti-Pattern
Directly inserting raw SQL rows into the database table without calculating the HMAC-SHA256 signature and linking the previous record's hash.

### ✅ Correct Pattern
Always route persistence through `IAuditStore`. The provider calculates the deterministic signature across monotonic fields (`Id`, `TenantId`, `Timestamp`, `ActorId`, `PayloadHash`, `PreviousRecordHash`), guaranteeing cryptographic tamper-evidence.

---

## 4. Cross-Tenant Ambient Audit Leakage

### ❌ Anti-Pattern
Relying on static singletons or global shared state for tenant resolution during asynchronous audit flushes.

### ✅ Correct Pattern
Capture the `TenantId` explicitly at `AuditScope` creation time and propagate it through immutable value objects. Ensure database providers enforce tenant isolation at the schema or query level (e.g. PostgreSQL RLS).

---

## 5. Non-Monotonic Random Identifier Generation

### ❌ Anti-Pattern
Using `Guid.NewGuid()` (UUIDv4) for audit records, causing severe index fragmentation in B-Tree clustered indexes on write-heavy append-only tables.

### ✅ Correct Pattern
Always use `AuditId` (UUIDv7), which embeds a high-precision millisecond timestamp in the leading 48 bits, ensuring sequential, monotonic index insertion and zero B-Tree page splits.
