# ADR-032: Deprecation of Polymorphic Reflection Overloads in StaticMediator

**Status**: Approved (Implemented in v1.0)

**Date**: 2026-08-26

**Supersedes**: —

**Superseded by**: —

---

## Context

`StaticMediator` provides a static dispatch alternative to the DI-hosted `IMediator`. Two overloads were introduced during early development:

```csharp
public static ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
public static ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);
```

These overloads use **runtime type discovery** (`object.GetType()`) to look up the registered handler from a `ConcurrentDictionary<Type, object>`. The handler object is then cast to an internal `ICommandInvoker<TResponse>` or `IQueryInvoker<TResponse>` interface and invoked via virtual dispatch.

> **Technical note (audit correction vs. original ADR draft):** The mechanism is `object.GetType()` for type-keyed dictionary lookup, followed by an interface cast (`is ICommandInvoker<TResponse> invoker`). The overloads do **not** use `MethodInfo.Invoke`. However, the use of `GetType()` on a covariant generic parameter prevents Native AOT trimmer from statically proving type safety, creating a semantic violation of ADR-010 (No Runtime Reflection for dispatch routing).

This is problematic because:
1. **AOT / Trimming**: `GetType()` in a generic dispatch context can cause trimmer warnings when the concrete type is not statically known.
2. **Type Safety**: The polymorphic dispatch silently routes to the handler of the runtime concrete type, which may differ from the declared static type. This can cause runtime surprises with polymorphic command hierarchies.
3. **ADR-010 violation**: ADR-010 rejected all runtime-reflection-based dispatch in favor of compile-time monomorphized switch tables.

The preferred alternatives are:
- `StaticMediator.SendCommand<TCommand, TResponse>(command, ct)` — type-safe, zero `GetType()`, AOT-safe.
- `StaticMediator.SendQuery<TQuery, TResponse>(query, ct)` — type-safe, zero `GetType()`, AOT-safe.

## Decision

Mark the polymorphic `Send<TResponse>(ICommand<TResponse>)` and `Send<TResponse>(IQuery<TResponse>)` overloads with `[Obsolete(error: false)]` in v1.0. The obsolete message directs consumers to the type-safe generic overloads. The overloads are retained for backward compatibility and will be removed in v2.0.

Code that references these overloads now generates `CS0618` compiler warnings, which are actionable at the IDE and CI level.

## Consequences

### Positive
- Eliminates runtime type ambiguity in static dispatching.
- Restores full AOT/trimming safety for `StaticMediator`.
- Provides clear migration path via `CS0618` warning.

### Negative
- Existing code using `StaticMediator.Send(myCommand)` receives `CS0618` warning and must migrate.
- Minor migration friction for early adopters.

### Neutral
- Breaking change is *soft* (`error: false`) — code continues to compile and run correctly.
- Hard removal is planned for v2.0 (semver major).
