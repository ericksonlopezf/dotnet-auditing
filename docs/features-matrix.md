# Master Feature Matrix & Architecture Roadmap

## 1. Feature Support Matrix

| Category | Capability | Status | Target |
|---|---|:---:|---|
| **CQRS Core** | Point-to-Point Command Dispatch (`ICommand<T>`) | ✅ Fully Supported | v1.0 |
| **CQRS Core** | Point-to-Point Query Dispatch (`IQuery<T>`) | ✅ Fully Supported | v1.0 |
| **CQRS Core** | Asynchronous Streaming Query Dispatch (`IStreamQuery<T>`) | ✅ Fully Supported | v1.0 |
| **Pub/Sub** | In-Process Domain Events (`INotification`) | ✅ Fully Supported | v1.0 |
| **Pub/Sub** | Configurable Notification Dispatch Strategies (`[PublishStrategy]`) | ✅ Fully Supported | v1.0 |
| **Pipelines** | Zero-Allocation Struct Pipeline Interceptors (`IPipelineBehavior`) | ✅ Fully Supported | v1.0 |
| **Pipelines** | Notification Middleware Interceptors (`INotificationBehavior`) | ✅ Fully Supported | v1.0 |
| **Middleware** | Native FluentValidation Integration (`ValidationPipelineBehavior`) | ✅ Fully Supported | v1.0 |
| **Middleware** | Microsoft Polly v8 Resilience Integration (`[UseResiliencePipeline]`) | ✅ Fully Supported | v1.0 |
| **Middleware** | OpenTelemetry Tracing & Metrics (`ActivitySource`, Meters) | ✅ Fully Supported | v1.0 |
| **Middleware** | In-Process Concurrency & Rate Limiting (`RateLimitingBehavior`) | ✅ Fully Supported | v1.0 |
| **Integrations** | Functional Error Short-Circuiting (`IResultFactory<TResponse>`) | ✅ Fully Supported | v1.0 |
| **Presentation** | ASP.NET Core Minimal API Extensions (`MapCommand`, `MapQuery`) | ✅ Fully Supported | v1.0 |
| **Quality Gates** | Roslyn Compile-Time Diagnostics (`ELM001` - `ELM011`) | ✅ Fully Supported | v1.0 |
| **Runtime** | 100% Native AOT & Trimming Verified | ✅ Fully Supported | v1.0 |
