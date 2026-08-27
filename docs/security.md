# Security Architecture & Supply Chain Verification

## 1. Zero Dynamic IL Vulnerabilities
Because dispatch logic is synthesized into static C# source code during build, runtime dynamic code injection vectors are eliminated.

## 2. Supply Chain Security
- Strong Named with `EricksonLopez.snk`.
- Sigstore OIDC cryptographic provenance on every NuGet release.
- Zero known vulnerabilities with continuous automated `NuGetAudit` scanning.
