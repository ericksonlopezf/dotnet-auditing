# ADR-0006: Global Sensitive Data Protection & Redaction Pipeline

## Context

Audit logs are frequent targets for data exfiltration. If application developers inadvertently pass objects containing passwords, API tokens, credit card numbers, or PII into audit changes, confidential data leaks into forensic storage, violating GDPR, PCI-DSS, and HIPAA regulations.

## Decision

The core pipeline integrates a dedicated `AuditSensitivityPipeline` that executes prior to persistence. It enforces:
1. An automatic built-in global denylist covering known secret keywords (passwords, tokens, API keys, private keys, credit cards, SSNs).
2. Support for custom denylist extensions via `AuditConfiguration.GlobalFieldDenylist`.
3. Support for explicit field redaction via `AuditChange.Redacted(fieldName)` (suppressing historical and new values while preserving the field name).
4. One-way SHA-256 cryptographic hashing via `AuditSensitivityPipeline.HashValue(plainText)`, which must be called **explicitly by the caller** when constructing `AuditChange` values. The `AuditFieldSensitivity.Hash` enum value documents this intent but is **not automatically enforced** by `AuditSensitivityPipeline.Apply()`; it is the caller's responsibility to invoke `HashValue()` and pass the resulting hex string as the change value.

## Consequences

### Positive
* Zero-leakage security invariant enforced transparently by the core pipeline.
* Prevents compliance violations even if application callers fail to sanitize domain objects before logging.

### Negative / Trade-offs
* Case-insensitive denylist string checks add a negligible processing overhead during audit change processing.
