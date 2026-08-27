# ADR-031: Institutional Testing Standard — Osherove Naming Pattern and Test-Scoped IDE1006 Suppression

**Status**: Approved (Implemented in v1.0)

**Date**: 2026-08-26

**Supersedes**: —

**Superseded by**: —

---

## Context

Unit and integration test method names serve as living, executable specifications. They are displayed verbatim in:
- IDE test runners (VS Test Explorer, Rider Unit Test explorer)
- CLI output (`dotnet test`)
- CI/CD pipeline reports
- Mutation testing reports (Stryker)

Standard C# PascalCase naming (`ShouldCreateUser()`) makes complex behavioral scenarios difficult to read without contextual separators. The Osherove naming pattern (`Method_Scenario_Result`) provides a consistent, three-part structure that improves specification readability without requiring additional documentation tools.

## Decision

Formally adopt the **Osherove naming standard** (`Method_Scenario_Result` / `UnitOfWork_StateUnderTest_ExpectedBehavior`) for all test methods across all test projects in the `EricksonLopez.Mediator` ecosystem.

Locally suppress analyzer warning `IDE1006` (non-PascalCase identifier) in all test projects and test files via `.editorconfig`, scoped explicitly to test projects. Production code remains strictly compliant with standard .NET PascalCase naming rules.

## Consequences

### Positive
- Test names act as living documentation: any developer can read test output and understand the behavioral contract without reading the implementation.
- Consistent with industry-standard test naming practices (xUnit, NUnit, MSTest communities).
- CI pipeline report clarity improves significantly.

### Negative
- Requires `IDE1006` suppression in `.editorconfig` for test projects.
- Minor naming discipline overhead for contributors unfamiliar with the pattern.

### Neutral
- No impact on production code.
- No impact on test execution semantics.
