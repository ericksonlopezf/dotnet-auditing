# ADR-0001: Append-Only Immutable Storage Contract (`IAuditStore`)

## Context

Audit logs in compliance-governed enterprise environments (SOC2, PCI-DSS, GDPR, HIPAA) serve as forensic evidence. Allowing audit records to be updated or deleted within the application SPI compromises non-repudiation and invites data tampering vulnerabilities.

## Decision

The `IAuditStore` interface strictly provides append (`AppendAsync`, `AppendBatchAsync`) and query (`QueryAsync`) capabilities. No `Update` or `Delete` APIs are exposed on the contract, and official SQL migration scripts enforce table permissions without update/delete grants. If an operation was executed mistakenly, compensating audit records must be emitted instead of mutating historical entries.

## Consequences

### Positive
* Non-repudiation compliance guarantee enforced at the API contract level.
* Simplified persistence logic in database adapters.
* Eliminates concurrency conflicts on historical audit entries.

### Negative / Trade-offs
* Correcting erroneous data requires recording an additional audit record with a compensating action.
