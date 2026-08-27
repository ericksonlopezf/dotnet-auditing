# Capability & Feature Support Matrix

| Capability | EricksonLopez.Mediator | MediatR 12.x | MassTransit (In-Proc) | Wolverine |
|---|:---:|:---:|:---:|:---:|
| **Compile-Time Static Dispatch Tables** | ✅ Yes (Roslyn Generator) | ❌ No (Reflection) | ❌ No | ⚠️ Partial |
| **Native AOT 100% Trimmable** | ✅ Yes | ❌ No | ❌ No | ⚠️ Partial |
| **0 B Allocation for 5-Behavior Pipeline** | ✅ Yes (0 B) | ❌ 480 B | ❌ 1.2 KB | ⚠️ 96 B |
| **Explicit CQRS Segregation (`ICommand` vs `IQuery`)** | ✅ Yes | ❌ No (`IRequest<T>`) | ❌ No | ⚠️ Partial |
| **Reactive Streaming Queries (`IStreamQuery<T>`)** | ✅ Yes (`IAsyncEnumerable`) | ✅ Yes | ❌ No | ❌ No |
| **Roslyn Compile-Time Diagnostics (`ELM001` - `ELM011`)** | ✅ Yes (11 Rules) | ❌ No | ❌ No | ❌ No |
| **Struct Continuations (`INext<T>`)** | ✅ Yes | ❌ No (Delegates) | ❌ No (Delegates) | ❌ No |
| **Polly v8 Resilience Integration** | ✅ Yes (`EricksonLopez.Mediator.Polly`) | ❌ Custom | ❌ Custom | ⚠️ Internal |
| **OpenTelemetry Native Tracing** | ✅ Yes (`EricksonLopez.Mediator.OpenTelemetry`) | ❌ Custom | ✅ Yes | ✅ Yes |
