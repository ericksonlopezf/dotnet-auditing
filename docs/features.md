# Core Features & Architecture Capabilities

## 1. Core Engine Capabilities
1. **Direct In-Memory Dispatch**: Sub-nanosecond message delivery directly to handlers via pre-compiled call chains.
2. **Sequential, Parallel & Aggregated Publishing**: Full control over notification execution with `Sequential`, `Parallel`, and `SequentialAggregateExceptions` strategies.
3. **Railway-Oriented Result Short-Circuiting**: Middleware behaviors (such as FluentValidation) can construct error results directly via `IResultFactory<TResponse>` without throwing exceptions.
4. **Compile-Time Roslyn Invariant Verifier**: Emits compile-time diagnostics (`ELM001` - `ELM011`) for missing handlers, duplicate registrations, and invalid signatures.
