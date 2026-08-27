# ADR-0004: Cryptographic Tamper-Evidence via HMAC-SHA256 Chaining

## Context

Audit logs stored in standard databases can be tampered with by privileged database administrators or compromised database credentials. Compliance frameworks require a mechanism to detect record alteration, deletion, or truncation.

## Decision

Provide a verifiable cryptographic integrity chain via `HmacAuditIntegrityService`. Each record computes a digest over canonical byte representations binding the record ID, timestamp, tenant ID, actor, action, resource, outcome, and the previous record's hash (`previous_hash`) using a tenant-specific HMAC-SHA256 secret key. Verification compares computed signatures using constant-time equality (`CryptographicOperations.FixedTimeEquals`) to eliminate timing side-channel attacks.

## Consequences

### Positive
* Any modification of an audited field, deletion of a historical record, or out-of-order insertion breaks the HMAC chain and is immediately detectable via `VerifyChainAsync()`.
* Constant-time verification protects against timing analysis attacks.

### Negative / Trade-offs
* Computing HMAC hashes introduces minimal CPU overhead during write operations.
* Requires secure tenant key management via an external KMS provider.
