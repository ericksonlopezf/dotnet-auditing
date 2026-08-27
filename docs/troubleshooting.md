# Troubleshooting & Compiler Diagnostics Guide

`EricksonLopez.Mediator` uses Roslyn Analyzers and Source Generators to validate mediator architecture at compile time.

---

## Diagnostic Reference Table

| Diagnostic ID | Severity | Title | Cause & Resolution |
|---|---|---|---|
| **`ELM001`** | `Error` | Handler Not Found | A command or query implements `ICommand<T>` or `IQuery<T>` but has no registered handler implementing `ICommandHandler` or `IQueryHandler`. **Fix**: Implement the missing handler class or use `[DiscoverHandlers]` if in another assembly. |
| **`ELM002`** | `Error` | Multiple Command Handlers | Multiple handler classes were found for the same command. CQRS requires exactly one handler per command. **Fix**: Remove or consolidate duplicate handlers. |
| **`ELM003`** | `Error` | Multiple Query Handlers | Multiple handler classes were found for the same query. Exactly one handler is allowed. **Fix**: Consolidate duplicate query handlers. |
| **`ELM004`** | `Error` | Invalid Handler Signature | A handler implements `ICommandHandler` or `IQueryHandler` but the `Handle` method has an incorrect signature or return type. **Fix**: Ensure `Handle` returns `ValueTask<TResponse>` and accepts `(TRequest req, CancellationToken ct)`. |
| **`ELM005`** | `Warning` | Open Generic Handler Not Supported | Open generic handlers cannot be statically resolved to concrete types at compile time. **Fix**: Create concrete closed handler subclasses. |
| **`ELM006`** | `Warning` | Notification Handler Not Found | A notification was declared but has no subscribers. It will be ignored when published. **Fix**: Implement `INotificationHandler<T>` if required. |
| **`ELM007`** | `Error` | Unsupported Open Generic Behavior | An open generic pipeline behavior has an unsupported number of type arguments or fails generic constraints. **Fix**: Ensure behaviors implement `IPipelineBehavior<TRequest, TResponse>` (2 type parameters) or `INotificationBehavior<TNotification>` (1 type parameter). |
| **`ELM008`** | `Warning` | Behavior Order Conflict | Two behaviors on the same request share the same explicit `Order` index, making execution order nondeterministic. **Fix**: Assign unique order integers in `[UseBehavior(typeof(B), order: N)]`. |
| **`ELM009`** | `Error` | Stream Handler Not Found | A stream request implements `IStreamRequest<T>` but has no registered handler implementing `IStreamRequestHandler<TReq, TResp>`. **Fix**: Implement the missing `IStreamRequestHandler`. |
| **`ELM010`** | `Error` | Multiple Stream Handlers | Multiple stream handlers found for the same `IStreamRequest<T>`. |
| **`ELM011`** | `Error` | Invalid Stream Handler Signature | Stream handler `Handle` method must return `IAsyncEnumerable<TResponse>`. |

---

## Common Scenarios

### 1. Handlers in External Assembly Not Found (`ELM001`)
By default, the Source Generator only inspects the current compilation assembly. If handlers reside in another project:
```csharp
[assembly: DiscoverHandlers(typeof(ExternalProjectMarker))]
```

### 2. Direct Streaming Dispatch
Streaming with `IStreamRequest<T>` and `ISender.CreateStream` dispatches directly to the concrete `IStreamRequestHandler` without allocating intermediate struct pipeline chains, maintaining zero-allocation execution and Native AOT compatibility.

