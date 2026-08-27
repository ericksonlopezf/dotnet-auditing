# ADR-033: Deprecation of EricksonLopez.Mediator.Validation in Favor of EricksonLopez.Mediator.FluentValidation

**Status**: Approved (Implemented in v1.0)

**Date**: 2026-08-26

**Supersedes**: —

**Superseded by**: —

---

## Context

`EricksonLopez.Mediator.Validation` was the original FluentValidation integration package for `EricksonLopez.Mediator`. It exposed:
- `ValidationBehavior<TRequest, TResponse>` — a pipeline behavior executing `IValidator<TRequest>` instances.
- `AddMediatorValidation()` — registers the open generic behavior.
- `AddMediatorValidatorsFromAssembly(assembly)` — scans assemblies for validators using `AssemblyScanner`, which relies on `[RequiresUnreferencedCode]` assembly reflection.

Issues identified during v1.0-rc1 audit:
1. **Package naming inconsistency**: The package name contains `Validation` which is a generic term; the purpose is specifically FluentValidation integration.
2. **False AOT claim**: The package metadata claimed `IsAotCompatible=true`, but `AddMediatorValidatorsFromAssembly` is annotated with `[RequiresUnreferencedCode]` — making the AOT claim incorrect for assembly-scanning scenarios.
3. **Consolidation opportunity**: A new package `EricksonLopez.Mediator.FluentValidation` was designed to supersede it with cleaner naming, accurate AOT documentation, and an AOT-safe explicit validator registration path (`AddMediatorFluentValidationValidator<TValidator, TRequest>()`).

## Decision

1. **Deprecate** `EricksonLopez.Mediator.Validation` by marking `ValidationBehavior<TRequest, TResponse>` and `AddMediatorValidation()` with `[Obsolete(error: false)]` directing consumers to `EricksonLopez.Mediator.FluentValidation`.
2. **Create** `EricksonLopez.Mediator.FluentValidation` package with:
   - `ValidationPipelineBehavior<TRequest, TResponse>` — AOT-safe behavior.
   - `AddMediatorFluentValidation()` — registers the behavior.
   - `AddMediatorFluentValidatorsFromAssembly(assembly)` — annotated with `[RequiresUnreferencedCode]` explicitly.
   - `AddMediatorFluentValidationValidator<TValidator, TRequest>()` — **AOT-safe** explicit registration path.
3. **Archive** `EricksonLopez.Mediator.Validation` in v2.0 (no longer published to NuGet).

## Migration Guide

```bash
# Remove deprecated package
dotnet remove package EricksonLopez.Mediator.Validation

# Add replacement
dotnet add package EricksonLopez.Mediator.FluentValidation
```

```csharp
// Before (deprecated):
services.AddMediatorValidation();
services.AddMediatorValidatorsFromAssembly(typeof(Program).Assembly);

// After (recommended):
services.AddMediatorFluentValidation();
services.AddMediatorFluentValidatorsFromAssembly(typeof(Program).Assembly);

// Or AOT-safe explicit registration:
services.AddMediatorFluentValidation();
services.AddMediatorFluentValidationValidator<CreateUserCommandValidator, CreateUserCommand>();
```

## Consequences

### Positive
- Accurate package naming aligned with its purpose (FluentValidation integration).
- Correct AOT documentation: assembly scanning is explicitly marked as not AOT-safe.
- New AOT-safe validator registration path available.
- Clean API surface for v2.0.

### Negative
- Breaking change for consumers of `EricksonLopez.Mediator.Validation` (soft — `[Obsolete(error: false)]`, hard remove in v2.0).
- Migration effort required before v2.0.

### Neutral
- Validation behavior logic is identical in both packages.
- `FluentValidation` and `EricksonLopez.Result.FluentValidation` dependencies remain the same.
