# Level 13: Diagnostics, Tracing & Metrics

`EricksonLopez.Mediator.OpenTelemetry` delivers zero-reflection distributed tracing and runtime metrics instrumentation.

---

## 1. OpenTelemetry Distributed Tracing & Metrics Setup

Configure OpenTelemetry in `Program.cs` by subscribing to both the `ActivitySource` and `Meter` named `"EricksonLopez.Mediator"`:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("MyMicroservice"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("EricksonLopez.Mediator") // Subscribe to Mediator spans
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("EricksonLopez.Mediator") // Subscribe to Mediator metrics
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter();
    });
```

---

## 2. Emitted Spans & Tags

Every command, query, and notification dispatched creates an OpenTelemetry `Activity` (span):

| Span Tag | Description | Example |
|---|---|---|
| `mediator.request.type` | Full CLR name of the command / query | `MyApp.Users.CreateUserCommand` |
| `mediator.handler.type` | Full CLR name of the executing handler | `MyApp.Users.CreateUserCommandHandler` |
| `mediator.status` | Execution outcome | `Success` or `Error` |
| `mediator.error.type` | Exception type on failure | `System.InvalidOperationException` |

---

## 3. Emitted Metrics Instruments

`EricksonLopez.Mediator.OpenTelemetry` records real-time metrics with zero reflection overhead:

| Metric Name | Instrument | Description | Tags |
|---|---|---|---|
| `mediator.request.duration` | Histogram | Request execution duration in milliseconds | `request_type`, `status` |
| `mediator.requests.total` | Counter | Total count of dispatched mediator requests | `request_type`, `status` |
| `mediator.notifications.total` | Counter | Total count of published notifications | `notification_type`, `strategy` |

---

**Congratulations!** You have completed the comprehensive showcase for `EricksonLopez.Mediator`. You are equipped to build lightning-fast, zero-allocation, Native AOT-ready CQRS systems in .NET.
