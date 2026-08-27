// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.AspNetCore;
using EricksonLopez.Mediator.FluentValidation;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EricksonLopez.Mediator.AspNetCore.Tests.Fixtures;

// ─── Contracts & Handlers ───────────────────────────────────────────────────

public record GetWeatherQuery(string City) : IQuery<WeatherResponse>;
public record WeatherResponse(string City, int Temperature);

public class GetWeatherQueryHandler : IQueryHandler<GetWeatherQuery, WeatherResponse>
{
    public ValueTask<WeatherResponse> Handle(GetWeatherQuery query, CancellationToken cancellationToken)
    {
        return new ValueTask<WeatherResponse>(new WeatherResponse(query.City, 25));
    }
}

public record CreateOrderCommand(string ProductName, decimal Price) : ICommand<OrderCreatedResponse>;
public record OrderCreatedResponse(Guid OrderId, string ProductName, decimal Price);

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderCreatedResponse>
{
    public ValueTask<OrderCreatedResponse> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ProductName))
        {
            throw new ArgumentException("Product name cannot be empty.", nameof(command.ProductName));
        }

        return new ValueTask<OrderCreatedResponse>(
            new OrderCreatedResponse(Guid.NewGuid(), command.ProductName, command.Price));
    }
}

public record OrderNotification(Guid OrderId) : INotification;

public class OrderNotificationHandler : INotificationHandler<OrderNotification>
{
    private readonly NotificationAuditLog _auditLog;
    public OrderNotificationHandler(NotificationAuditLog auditLog) => _auditLog = auditLog;

    public ValueTask Handle(OrderNotification notification, CancellationToken cancellationToken)
    {
        _auditLog.Record(notification.OrderId);
        return default;
    }
}

[UseBehavior(typeof(ValidationPipelineBehavior<ValidateOrderCommand, Result<Guid>>))]
public record ValidateOrderCommand(string ItemName, int Quantity) : ICommand<Result<Guid>>;

public class ValidateOrderCommandHandler : ICommandHandler<ValidateOrderCommand, Result<Guid>>
{
    public ValueTask<Result<Guid>> Handle(ValidateOrderCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }
}

public class ValidateOrderCommandValidator : AbstractValidator<ValidateOrderCommand>
{
    public ValidateOrderCommandValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().WithMessage("Item name is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
    }
}

public class ValidateOrderResultFactory : IResultFactory<Result<Guid>>
{
    public Result<Guid> CreateFailure(Error error) => Result<Guid>.Failure(error);
}

public class NotificationAuditLog
{
    public ConcurrentBag<Guid> Received { get; } = new();
    public void Record(Guid id) => Received.Add(id);
}

public record FaultyCommand : ICommand<string>;
public class FaultyCommandHandler : ICommandHandler<FaultyCommand, string>
{
    public ValueTask<string> Handle(FaultyCommand command, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Simulated internal handler exception");
}

public record GetScopedIdQuery : IQuery<Guid>;
[ServiceLifetime(HandlerLifetime.Scoped)]
public class ScopedIdQueryHandler : IQueryHandler<GetScopedIdQuery, Guid>
{
    private readonly Guid _instanceId = Guid.NewGuid();
    public ValueTask<Guid> Handle(GetScopedIdQuery query, CancellationToken cancellationToken) => new(_instanceId);
}

public record StreamNumbersRequest(int Count) : IStreamRequest<int>;
public class StreamNumbersRequestHandler : IStreamRequestHandler<StreamNumbersRequest, int>
{
    public async IAsyncEnumerable<int> Handle(StreamNumbersRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

public record CancellableCommand(int DelayMs) : ICommand<string>;
public class CancellableCommandHandler : ICommandHandler<CancellableCommand, string>
{
    public async ValueTask<string> Handle(CancellableCommand command, CancellationToken cancellationToken)
    {
        if (command.DelayMs > 0)
        {
            await Task.Delay(command.DelayMs, cancellationToken);
        }
        return "CancellableCompleted";
    }
}

// ─── Test Server Setup ────────────────────────────────────────────────────────

public sealed class MediatorApplicationFactory : IDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public MediatorApplicationFactory()
    {
        var builder = WebApplication.CreateBuilder(new string[] { "--environment", "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<NotificationAuditLog>();
        builder.Services.AddSingleton<IValidator<ValidateOrderCommand>, ValidateOrderCommandValidator>();
        builder.Services.AddSingleton<IResultFactory<Result<Guid>>, ValidateOrderResultFactory>();
        builder.Services.AddEricksonLopezMediator();
        builder.Services.AddTransient<ValidationPipelineBehavior<ValidateOrderCommand, Result<Guid>>>();
        builder.Services.AddTransient<IPipelineBehavior<ValidateOrderCommand, Result<Guid>>, ValidationPipelineBehavior<ValidateOrderCommand, Result<Guid>>>();

        _app = builder.Build();

        // Standard minimal API routes
        _app.MapGet("/weather/{city}", async (string city, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetWeatherQuery(city));
            return Results.Ok(result);
        });

        _app.MapPost("/orders", async (CreateOrderCommand command, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/orders/{result.OrderId}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        _app.MapPost("/orders/validated", async (ValidateOrderCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (result.IsFailure)
            {
                return Results.BadRequest(new { code = result.Error.Code, description = result.Error.Description });
            }
            return Results.Ok(new { orderId = result.Value });
        });

        // EricksonLopez.Mediator.AspNetCore extension methods
        _app.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/api/orders");
        _app.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/api/orders/put", "PUT");
        _app.MapCommand<CancellableCommand, string>("/api/cancellable");
        _app.MapQuery<GetWeatherQuery, WeatherResponse>("/api/weather");

        _app.MapPost("/orders/notify", async (OrderNotification notification, IMediator mediator) =>
        {
            await mediator.Publish(notification);
            return Results.Ok(new { status = "published" });
        });

        _app.MapGet("/faulty", async (IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new FaultyCommand());
                return Results.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        _app.MapGet("/scoped-id", async (IMediator mediator) =>
        {
            var id1 = await mediator.Send(new GetScopedIdQuery());
            var id2 = await mediator.Send(new GetScopedIdQuery());
            return Results.Ok(new { id1, id2 });
        });

        _app.MapGet("/stream/{count:int}", (int count, IMediator mediator) =>
        {
            return Results.Ok(mediator.CreateStream(new StreamNumbersRequest(count)));
        });

        _app.Start();
        _client = _app.GetTestClient();
    }

    public HttpClient CreateClient() => _client;
    public IServiceProvider Services => _app.Services;
    public IEndpointRouteBuilder App => _app;

    public void Dispose()
    {
        _client.Dispose();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
    }
}
