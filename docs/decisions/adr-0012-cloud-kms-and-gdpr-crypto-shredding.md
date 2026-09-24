<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0012: Cloud KMS Integration and GDPR Crypto-Shredding (`IAuditCryptoKeyProvider`)

## Status
Accepted

## Date
2026-09-14

## Context
Enterprise compliance standards mandate strict security for cryptographic keys used in HMAC-SHA256 audit chaining, while data privacy regulations like GDPR (Article 17 "Right to Erasure") require personal data to be permanently erased upon request:
1. **Key Storage & Rotation**: Storing cryptographic keys in application configuration or local disk creates severe security risks and prevents coordinated key rotation in distributed deployments.
2. **Immutable Log Conflict with Erasure**: Audit records are immutable and cryptographically chained by design (ADR-0001, ADR-0004). Deleting rows or updating PII fields breaks the cryptographic hash chain, destroying legal tamper-evidence for all subsequent records in the chain.

## Decision
1. **Azure Key Vault HSM Key Provider (`EricksonLopez.Auditing.AzureKeyVault`)**:
   - Introduce `AzureKeyVaultIntegrityProvider` implementing `IAuditIntegrityProvider` and `IAuditCryptoKeyProvider`.
   - Fetches tenant-scoped secret keys directly from Azure Key Vault using managed identity (`Azure.Identity`), eliminating hardcoded keys and enabling automated rotation.
2. **Crypto-Shredding Pattern via `IAuditCryptoKeyProvider`**:
   - Provide `IAuditCryptoKeyProvider` with key retrieval (`GetKeyAsync`) and explicit key destruction (`ShredKeyAsync`).
   - Sensitive personal identifiers and PII changes are encrypted using subject-specific or tenant-specific encryption keys.
   - When an erasure request is processed, the subject's encryption key is cryptographically shredded via `ShredKeyAsync`.
   - The encrypted payload in historical audit records becomes permanently unrecoverable ciphertext, satisfying GDPR Article 17 requirements without deleting rows, modifying byte sequences, or breaking the HMAC-SHA256 hash chain.

## Consequences

### Positive
* Hardware-security module (HSM) backed audit key protection in cloud environments.
* Seamless regulatory reconciliation: satisfies GDPR Article 17 "Right to Erasure" while preserving SOC2/PCI-DSS immutable forensic chains.
* Zero broken hash links during privacy compliance operations.
* Multi-tenant secret segregation supported natively.

### Negative / Trade-offs
* Dependent on Azure Key Vault or external KMS connectivity for initial key resolution (cached locally per tenant session).
* Key destruction (`ShredKeyAsync`) is irreversible; once shredded, original plaintext values cannot be recovered even for legal discovery.
