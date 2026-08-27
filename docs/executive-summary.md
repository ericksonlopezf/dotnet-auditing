# Executive Summary — EricksonLopez.Mediator

`EricksonLopez.Mediator` is the reference in-process mediator and CQRS dispatching framework designed specifically for high-throughput, low-latency .NET applications and Native AOT deployments.

---

## Strategic Pillars

1. **Native AOT & Zero Reflection**: Designed from inception without any runtime reflection (`System.Reflection`), emitting 0 trimming warnings under `PublishAot=true`.
2. **Zero-Allocation Pipeline Architecture**: Replaces traditional delegate closures with unboxed, inlinable `struct INext<TResponse>` continuations.
3. **Compile-Time Roslyn Verification**: Verifies handler signatures, single-handler constraints, and behavior chains at compile time via analyzer diagnostics (`ELM001` - `ELM011`).
4. **Architectural Purity**: Clean separation between core abstractions (`EricksonLopez.Mediator`), Result pattern integrations (`EricksonLopez.Mediator.Result`), and observability (`EricksonLopez.Mediator.OpenTelemetry`).
