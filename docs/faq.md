# FAQ — EricksonLopez.Mediator

Frequently asked questions about `EricksonLopez.Mediator`.

---

## General

### Q: What is EricksonLopez.Mediator?

**A:** `EricksonLopez.Mediator` is an ultra-high-performance, zero-allocation, 100% Native AOT-compatible CQRS mediator library for .NET 8, 9, and 10. It replaces runtime reflection with Roslyn Incremental Source Generators that generate all handler dispatch, pipeline wiring, and DI registration at compile time.

---

### Q: How is this different from MediatR?

**A:** Key differences:

| Feature | EricksonLopez.Mediator | MediatR |
|---|---|---|
| Dispatch | Roslyn Source Generator (compile time) | Runtime reflection |
| Allocations | 0 B in hot path | 48-112 B per request |
| Native AOT | 100% compatible | Fails under trimming |
| CQRS enforcement | Separate ICommand/IQuery | Single IRequest |
| Compile-time diagnostics | ELM001-ELM011 | None |
| Pipeline continuation | Struct INext<T> (value type) | Delegate (closure) |

---

### Q: Does it require dependency injection?

**A:** No. For environments without a DI container, use `StaticMediator`:

```csharp
StaticMediator.RegisterCommandHandler(new MyCommandHandler());
var result = await StaticMediator.SendCommand<MyCommand, MyResponse>(new MyCommand("x"));
```

For standard DI environments, call `services.AddEricksonLopezMediator()`.

---

### Q: Is it compatible with Native AOT?

**A:** Yes — 100%. Zero runtime reflection. All dispatch is compiled into `GeneratedMediator.g.cs`.

---

### Q: Which .NET versions are supported?

**A:** .NET 8.0, 9.0, and 10.0. Both JIT and Native AOT are fully supported.

---

## Handlers

### Q: How does the Source Generator discover my handlers?

**A:** The Roslyn generator scans the current compilation for all handler implementations. Handlers in external assemblies require:

```csharp
[assembly: DiscoverHandlers(typeof(ExternalAssemblyMarker))]
```

---

### Q: Can I have multiple handlers for the same command?

**A:** No. Commands and queries must have exactly one handler. The generator produces compile-time error ELM002 or ELM003 for duplicates.

---

### Q: Can I have multiple handlers for the same notification?

**A:** Yes. INotification supports fan-out to zero, one, or many INotificationHandler<T> implementations. Use [PublishStrategy] to control execution order.

---

### Q: What DI lifetime does a handler get by default?

**A:** Transient. Override with [ServiceLifetime(HandlerLifetime.Singleton/Scoped/Transient)].

---

## Pipeline and Behaviors

### Q: What is the execution order of pipeline behaviors?

**A:** Behaviors execute in ascending order value. Lower order = outermost (executes first).

---

### Q: What is INext<TResponse> and why is it a struct?

**A:** INext<TResponse> is the pipeline continuation contract implemented as an internal readonly struct. This eliminates heap allocation per pipeline invocation. Always constrain as `where TNext : struct, INext<TResponse>`.

---

### Q: Can I use INotificationBehavior<T> alongside IPipelineBehavior<T,R>?

**A:** Yes — they are independent pipelines. IPipelineBehavior wraps command/query handlers; INotificationBehavior wraps notification handler invocations. Both use struct INext continuations.

---

## Validation

### Q: What validation attributes are available?

**A:** [ValidateRequest] + per-property: [ValidateNotNull], [ValidateNotEmpty], [ValidateRange(min,max)], [ValidateLength(min,max)], [ValidateRegex(pattern)].

---

### Q: Should I use [ValidateRequest] or FluentValidation?

**A:** Use [ValidateRequest] for simple compile-time constraints. Use EricksonLopez.Mediator.FluentValidation for complex cross-property rules.

---

## Testing

### Q: How do I unit test a service that uses IMediator?

**A:** Use FakeMediator from EricksonLopez.Mediator.Testing — no reflection, no Moq, Native AOT compatible.

---

### Q: How do I unit test a custom IPipelineBehavior in isolation?

**A:** Use DelegateNext<TResponse> to stub the continuation without a DI container.

---

### Q: What is FakeAssertionException?

**A:** Thrown when a FakeMediator assertion fails (e.g., ShouldHaveReceived<T>() with no matching received request).

---

## Performance

### Q: What does '0 B allocated' mean exactly?

**A:** On the synchronous hot path, the dispatch, pipeline traversal, and handler invocation allocate zero bytes on the managed heap. Verified by BenchmarkDotNet.

---

### Q: Should I register stateless handlers as Singleton?

**A:** Yes. If a handler has no instance fields, [ServiceLifetime(HandlerLifetime.Singleton)] eliminates DI instantiation overhead per dispatch.

---

## Ecosystem

### Q: Can I use it with ASP.NET Core Minimal APIs?

**A:** Yes. app.MapCommand<TCommand, TResponse>("/path") and app.MapQuery<TQuery, TResponse>("/path").

---

### Q: Does it support OpenTelemetry?

**A:** Yes. services.AddMediatorOpenTelemetry(options => { options.ActivitySourceName = "MyApp"; }).

---

### Q: What happened to EricksonLopez.Mediator.Validation?

**A:** Deprecated (ADR-033). Will be archived in v2.0. Use EricksonLopez.Mediator.FluentValidation instead.

---

## Troubleshooting

### Q: I get ELM001: No handler found. Why?

**A:** Either the handler is not implemented, is in a separate assembly without [DiscoverHandlers], or is not public.

### Q: Why does StaticMediator.SendCommand throw InvalidOperationException?

**A:** No handler registered. Call StaticMediator.RegisterCommandHandler() first.

### Q: My behavior is not executing. What is wrong?

**A:** Check [UseGlobalBehavior] or [UseBehavior] attribute placement, order uniqueness (ELM008), and that the behavior has the correct generic constraint.
