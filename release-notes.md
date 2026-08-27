# Release Notes

## Version 1.0.0 (General Availability) - 2026-08-26

We are thrilled to announce the official General Availability (GA) release of **EricksonLopez.Mediator 1.0.0**, a high-performance, Native AOT compatible, zero-allocation CQRS dispatching framework for .NET 10, .NET 9, and .NET 8.

### Highlights
- **Zero-Allocation Pipelines:** Compile-time monomorphized pipeline execution using nested `struct` continuations (`INext<T>`, `INext`), resulting in 0 memory allocations on the heap for `Send` calls.
- **Full Multi-Targeting:** Built and tested across `.NET 8.0` (LTS), `.NET 9.0` (STS), and `.NET 10.0`, with `netstandard2.0` support for the Roslyn Source Generator.
- **Roslyn Incremental Generator:** Automatically generates monomorphized dispatch tables and `services.AddEricksonLopezMediator()` DI registration at compile-time with zero runtime reflection.
- **Modular Ecosystem:** Dedicated packages for `AspNetCore`, `FluentValidation`, `OpenTelemetry`, `Polly`, `RateLimiting`, `Result`, and `Testing`.
- **Advanced Notification Strategies:** Opt-in Parallel Dispatch (`[PublishStrategy(PublishStrategy.Parallel)]`) and Aggregate Exception handling (`[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]`).
- **Rich IDE Diagnostics:** Real-time compile-time diagnostics (`ELM001`–`ELM011`) directly within Visual Studio and VS Code.

Available on NuGet today!

---