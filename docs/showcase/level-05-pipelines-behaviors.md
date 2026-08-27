# Level 05: Pipelines & Zero-Allocation Behaviors

Pipeline behaviors act as middleware that surrounds your command and query executions. They are ideal for cross-cutting concerns:
- Distributed tracing and metrics
- Logging and timing
- Validation and short-circuiting
- Resilience policies (retry, circuit breaker, timeout)
- Concurrency rate limiting

Unlike traditional delegate-based pipelines that allocate delegate and closure objects on every request, `EricksonLopez.Mediator` uses **unboxed struct continuations (`INext<TResponse>`)** to achieve **zero heap allocations** on the synchronous execution path.

---

## 1. Defining a Pipeline Behavior

Implement `IPipelineBehavior<TRequest, TResponse>` with the generic `where TNext : struct, INext<TResponse>` constraint:

```csharp
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using Microsoft.Extensions.Logging;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request, 
        TNext next, 
        CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("[Starting] {RequestName}", requestName);
        var timer = Stopwatch.StartNew();

        try
        {
            // Call next behavior or target handler (0 allocations)
            var response = await next.InvokeAsync().ConfigureAwait(false);
            
            timer.Stop();
            _logger.LogInformation("[Completed] {RequestName} in {ElapsedMs}ms", requestName, timer.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            timer.Stop();
            _logger.LogError(ex, "[Failed] {RequestName} after {ElapsedMs}ms", requestName, timer.ElapsedMilliseconds);
            throw;
        }
    }
}
```

---

## 2. Registering Behaviors

Behaviors are registered at compile time using attributes:

### Global Behaviors (Assembly-level)
Execute for every command and query in the assembly:

```csharp
using EricksonLopez.Mediator;

[assembly: UseGlobalBehavior(typeof(LoggingBehavior<,>), order: 1)]
[assembly: UseGlobalBehavior(typeof(MetricsBehavior<,>), order: 2)]
```

### Request-Specific Behaviors
Execute only for specific commands or queries:

```csharp
using EricksonLopez.Mediator;

[UseBehavior(typeof(ValidationBehavior<,>), order: 1)]
[UseBehavior(typeof(OrderLockingBehavior<,>), order: 2)]
public sealed record ProcessPaymentCommand(Guid OrderId, decimal Amount) : ICommand<PaymentResult>;
```

---

## 3. Deterministic Execution Order

Behaviors execute in ascending order based on their `order` index:
1. Global Behaviors (`order: 1`, `order: 2`, ...)
2. Request-Specific Behaviors (`order: 1`, `order: 2`, ...)
3. **The Target Handler**
4. Request-Specific Behaviors unwinding
5. Global Behaviors unwinding

The Roslyn compiler emits diagnostic **`ELM008`** if two behaviors share the same order index on the same request type, preventing nondeterministic execution orders.
