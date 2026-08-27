# Security Policy

`EricksonLopez.Auditing` is designed for compliance-driven, enterprise-grade audit logging where security, non-repudiation, tenant isolation, and forensic data integrity are paramount.

---

## Supported Versions

Only the latest active major and minor releases receive security updates and bug fixes:

| Version | Supported          | Target Frameworks           |
| :---    | :---:              | :---                        |
| `2.x`   | :white_check_mark: | .NET 8.0, .NET 9.0, .NET 10.0 |
| `1.x`   | :x:                | .NET 8.0                    |

---

## Reporting a Vulnerability

We take the security of `EricksonLopez.Auditing` very seriously. If you discover a potential security vulnerability, please do **NOT** open a public issue on GitHub.

Instead, please send an encrypted or direct email to:

* **Email:** [ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com)
* **Subject:** `[SECURITY] Vulnerability Report in EricksonLopez.Auditing`

### Please Include in Your Report:

1. Description of the vulnerability and its potential impact.
2. Steps or minimal code snippet to reproduce the issue.
3. Affected package(s), versions, and runtime environments (.NET 8/9/10, Native AOT, database engine).
4. Any proposed fixes or mitigations (if available).

### Response Timeline:

* **Initial Response:** Within 48 hours of receipt.
* **Triage & Status Update:** Within 5 business days.
* **Fix & Advisory Release:** Coordinated security advisory and patched NuGet packages released as soon as verified.

---

## Supply Chain Security

To ensure consumers can safely verify and consume packages, the following controls are enforced in the [publish workflow](.github/workflows/publish.yml):

### Sigstore Provenance Attestation

Every published NuGet package is accompanied by a signed SLSA provenance statement using [sigstore/gh-action-sigstore-python](https://github.com/sigstore/gh-action-sigstore-python) via `actions/attest-build-provenance@v2`. This creates a cryptographically verifiable link between the published `.nupkg` artifact and the exact GitHub Actions run that produced it.

Consumers can verify the provenance of any package using the GitHub CLI:

```bash
gh attestation verify <package.nupkg> --owner ericksonlopezf
```

### NuGet Trusted Publishing (OIDC)

Packages are published to NuGet.org using **NuGet Trusted Publishing** via OIDC token exchange (`NuGet/login@v1`). This eliminates the need for long-lived API key secrets in CI. The publish job requests a short-lived OIDC token scoped to the repository, which NuGet.org verifies before accepting the package push.

* **No static `NUGET_API_KEY` secrets are stored or used.**
* Requires NuGet Trusted Publisher configuration on nuget.org for `ericksonlopezf/dotnet-auditing`.

### SourceLink & Deterministic Builds

All packages are built with:
* `PublishRepositoryUrl=true` — embeds the source repository URL in the package.
* `EmbedUntrackedSources=true` — ensures all source files are tracked.
* `ContinuousIntegrationBuild=true` — enables deterministic builds in CI.
* Symbol packages (`.snupkg`) are published alongside every `.nupkg` with portable PDBs.

### Dependency Hygiene

* Zero external runtime dependencies in `EricksonLopez.Auditing.Abstractions`.
* All direct database dependencies in storage adapters are tracked centrally via Central Package Management (`Directory.Packages.props`).

---

## Core Security Boundaries & Guarantees

### 1. Multi-Tenant Isolation
* `TenantId` is mandatory in `AuditContext`. Platform-wide administrative events must explicitly specify `AuditContext.SystemTenantId` (`"system"`).
* Relational database adapters enforce tenant isolation at the database layer (e.g., PostgreSQL `FORCE ROW LEVEL SECURITY`, SQL Server `SESSION_CONTEXT` security policies, Oracle `SYS_CONTEXT` Virtual Private Database).
* Batch insertions strictly reject mixed-tenant collections with `InvalidOperationException`.

### 2. Cryptographic Tamper-Evidence
* The `HmacAuditIntegrityService` uses HMAC-SHA256 with tenant-scoped cryptographic keys.
* Hash verification uses `CryptographicOperations.FixedTimeEquals` to prevent side-channel timing attacks.
* Canonical byte representation binds record identifiers, timestamps, actor, action, resource, outcome, and predecessor hash.

### 3. Sensitive Data Protection (Zero-Leakage Invariant)
* The `AuditSensitivityPipeline` automatically filters known sensitive field names (passwords, tokens, private keys, API secrets, credit card numbers, SSNs, PINs).
* Explicit redaction (`AuditChange.Redacted(field)`) suppresses values while preserving audit proof of change.
* One-way cryptographic hashing (`AuditSensitivityPipeline.HashValue(plainText)`) enables equality matching without storing plain-text secrets.

### 4. Error Code Boundary
* `AuditRecord.ErrorCode` must strictly contain structured domain error identifiers (e.g., `AUTHZ_FORBIDDEN_RESOURCE`).
* Applications must never pass raw exception messages, stack traces, or connection strings into `ErrorCode`.
