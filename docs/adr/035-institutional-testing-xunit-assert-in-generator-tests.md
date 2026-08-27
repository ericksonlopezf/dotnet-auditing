# ADR-035: Institutional Testing Standard — Use of xUnit Assert in Source Generator Tests

**Status**: Approved (Implemented in v1.0)

**Date**: 2026-08-26

**Supersedes**: —

**Superseded by**: —

---

## Context

The `EricksonLopez.Mediator` ecosystem uses `AwesomeAssertions` (a FluentAssertions fork) as the standard assertion library across all test projects. This provides fluent, readable assertion chains consistent with the Osherove naming pattern adopted in ADR-031.

However, `EricksonLopez.Mediator.Generator.Tests` — the test project for the Roslyn Incremental Source Generator — contains 400+ test assertions that verify exact C# source code strings, regex patterns against generated output, and diagnostic message strings.

During a QA audit, it was noted that `Generator.Tests` uses native `xunit.Assert.*` instead of `AwesomeAssertions`.

Migrating `Generator.Tests` to `AwesomeAssertions` would require:
1. Rewriting 400+ string/regex assertions to `AwesomeAssertions` equivalents.
2. Risk of subtle breakage in Roslyn generator string verification tests, where exact whitespace and formatting are semantically significant.
3. Loss of `Assert.Contains(string, string)` for substring matching in generated source — which has no direct AwesomeAssertions equivalent without lambda-based predicates.

The risk/reward ratio of this migration is unfavorable. The inconsistency is isolated to one test project and does not affect production code or other test projects.

## Decision

**Retain `xunit.Assert.*` exclusively for `EricksonLopez.Mediator.Generator.Tests`** to prevent string/regex compilation breakage.

All other test projects (`Tests`, `IntegrationTests`, `Benchmarks`) **MUST** use `AwesomeAssertions` for all assertions.

This exception is explicitly documented here as an institutional decision — not an oversight.

## Consequences

### Positive
- Prevents high-risk migration of 400+ Roslyn string verification tests.
- Retains the existing passing test suite as a safety net during further development.
- Documents the exception explicitly, eliminating confusion in code reviews.

### Negative
- `Generator.Tests` uses a different assertion style than the rest of the test suite.
- New contributors must be aware of the two-assertion-style convention.

### Neutral
- No impact on test execution semantics or CI reliability.
- Both `Assert.*` and `AwesomeAssertions` produce compatible test results in xUnit runners.
