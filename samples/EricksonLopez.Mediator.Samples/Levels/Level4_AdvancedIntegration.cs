// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;

namespace Sample.Levels.Level4_AdvancedIntegration;

// --- Response DTO ---
public sealed record OrderSubmissionResult(Guid OrderId, string TrackingNumber);

// --- 1. Pipeline Behavior with Result Pattern Short-Circuiting ---
public sealed class OrderValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IResultFactory<TResponse>? _resultFactory;

    public OrderValidationBehavior(IResultFactory<TResponse>? resultFactory = null)
    {
        _resultFactory = resultFactory;
    }

    public ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        if (request is SubmitOrderCommand cmd)
        {
            // Business rule short-circuit (discontinued item)
            if (cmd.Sku.StartsWith("DISCONTINUED", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Level 4 - Pipeline] Short-circuit: SKU '{cmd.Sku}' is discontinued. Returning failure via IResultFactory.");
                if (_resultFactory is not null)
                {
                    var error = Error.Failure("Order.DiscontinuedProduct", $"Product SKU '{cmd.Sku}' is discontinued.");
                    return new ValueTask<TResponse>(_resultFactory.CreateFailure(error));
                }
            }
        }

        return next.InvokeAsync();
    }
}

// --- 2. Command decorated with Behavior and Declarative Attributes ---
[UseBehavior(typeof(OrderValidationBehavior<,>))]
[ValidateRequest]
public sealed record SubmitOrderCommand(
    [property: ValidateNotEmpty] string Sku,
    [property: ValidateRange(1, 1000)] int Quantity,
    [property: ValidateLength(3, 50)] string CustomerName) : ICommand<Result<OrderSubmissionResult>>;

/// <summary>
/// Command handler that mutates domain state.
/// </summary>
public sealed class SubmitOrderCommandHandler : ICommandHandler<SubmitOrderCommand, Result<OrderSubmissionResult>>
{
    public ValueTask<Result<OrderSubmissionResult>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 4 - Handler] Order processed successfully for '{command.CustomerName}' (SKU: {command.Sku}, Qty: {command.Quantity})");
        var result = new OrderSubmissionResult(Guid.NewGuid(), $"TRK-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}");
        return ValueTask.FromResult(Result<OrderSubmissionResult>.Success(result));
    }
}

/// <summary>
/// Level 4: Advanced Integration (Result Pattern, IResultFactory, and Declarative Validation).
/// </summary>
public static class Demo
{
    public static async Task RunAsync(IMediator mediator)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 4: ADVANCED INTEGRATION (RESULT PATTERN & DECLARATIVE VALIDATION)");
        Console.WriteLine("================================================================================");

        // 1. Valid Request
        Console.WriteLine("1. Execution with Valid Data (Successful Return):");
        var validCmd = new SubmitOrderCommand("SKU-LAPTOP-01", 2, "Alice Enterprise Corp");
        var successRes = await mediator.Send(validCmd, CancellationToken.None);
        Console.WriteLine($"   -> IsSuccess: {successRes.IsSuccess}, Tracking: {successRes.Value?.TrackingNumber}");
        Console.WriteLine();

        // 2. Business Rule Short-Circuiting with IResultFactory (No exceptions thrown)
        Console.WriteLine("2. Business Rule Pipeline Short-Circuiting via IResultFactory (No Exceptions):");
        var businessFailCmd = new SubmitOrderCommand("DISCONTINUED-MODEL-X", 1, "Alice Enterprise Corp");
        var businessFailRes = await mediator.Send(businessFailCmd, CancellationToken.None);
        Console.WriteLine($"   -> IsSuccess: {businessFailRes.IsSuccess}");
        if (businessFailRes.IsFailure)
        {
            Console.WriteLine($"   -> Error Code: {businessFailRes.Error.Code}");
            Console.WriteLine($"   -> Error Description: {businessFailRes.Error.Description}");
        }
        Console.WriteLine();

        // 3. Declarative Compile-Time Validation ([ValidateRequest] / [ValidateRange])
        Console.WriteLine("3. Compile-Time Generated Validation ([ValidateRequest] / [ValidateRange]):");
        try
        {
            var invalidRangeCmd = new SubmitOrderCommand("SKU-LAPTOP-01", -10, "Alice Enterprise Corp");
            await mediator.Send(invalidRangeCmd, CancellationToken.None);
        }
        catch (MediatorValidationException valEx)
        {
            Console.WriteLine($"   -> Caught MediatorValidationException: {valEx.Message}");
            Console.WriteLine($"   -> Error Count: {valEx.Errors.Count}");
        }

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}
