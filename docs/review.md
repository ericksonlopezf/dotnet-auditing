# Architectural Review & Continuous Audit Process

All pull requests must pass `./scripts/verify-compliance.ps1` before review. The script validates 8 zero-tolerance quality gates covering documentation, licensing, single-type-per-file, URLs, and warning suppressions.
