# Level 06: Error Handling & Result Pattern

`EricksonLopez.Mediator` provides two robust paradigms for error handling: Pipeline Exception Interception and AOT-Safe Result Pattern Short-Circuiting with `IResultFactory<TResponse>`.

---

## 1. Exception Interception via Pipeline Behaviors

You can intercept unhandled exceptions in a centralized pipeline behavior without cluttering domain handlers with `try/catch` boilerplate:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using Microsoft.Extensions.Logging;

public sealed class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    public ExceptionHandlingBehavior(ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request, 
        TNext next, 
        CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        try
        {
            return await next.InvokeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while executing {RequestType}", typeof(TRequest).Name);
            throw;
        }
    }
}
```

---

## 2. AOT-Safe Result Pattern Short-Circuiting (`EricksonLopez.Mediator.Result`)

Rather than throwing exceptions for domain errors or validation failures, returning a `Result<T>` from `EricksonLopez.Result` allows type-safe, Railway-Oriented error propagation.

To short-circuit a pipeline behavior without using runtime reflection (`Activator.CreateInstance` or `MakeGenericType`), `EricksonLopez.Mediator.Result` provides the `IResultFactory<TResponse>` interface (ADR-005 & ADR-026):

```csharp
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;
using FluentValidation;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IValidator<TRequest>? _validator;
    private readonly IResultFactory<TResponse>? _resultFactory;

    public ValidationBehavior(
        IValidator<TRequest>? validator = null, 
        IResultFactory<TResponse>? resultFactory = null)
    {
        _validator = validator;
        _resultFactory = resultFactory;
    }

    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request, 
        TNext next, 
        CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        if (_validator is not null)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                var error = Error.Validation("Request.ValidationFailed", validationResult.ToString());

                // Short-circuit without exceptions or reflection:
                if (_resultFactory is not null)
                {
                    return _resultFactory.CreateFailure(error);
                }

                throw new ValidationException(validationResult.Errors);
            }
        }

        return await next.InvokeAsync().ConfigureAwait(false);
    }
}
```

---

## 3. Benefits of `IResultFactory<TResponse>`

1. **Zero Runtime Reflection**: Avoids `Type.MakeGenericType` and `Activator.CreateInstance`, preserving 100% Native AOT compatibility.
2. **Predictable Flow**: Domain errors return HTTP 400/422 responses cleanly without expensive stack trace unwinding.
3. **Monomorphic Dispatch**: The Source Generator resolves `IResultFactory<Result<T>>` directly in DI.
