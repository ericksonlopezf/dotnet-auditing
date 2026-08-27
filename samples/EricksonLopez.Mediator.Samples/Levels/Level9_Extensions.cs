// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.AspNetCore;
using EricksonLopez.Mediator.HealthChecks;
using EricksonLopez.Mediator.OpenTelemetry;
using EricksonLopez.Mediator.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sample.Levels.Level9_Extensions;

// --- Extension Demonstration DTOs ---
public sealed record CheckStatusQuery(string ServiceName) : IQuery<string>;

public sealed class CheckStatusQueryHandler : IQueryHandler<CheckStatusQuery, string>
{
    public ValueTask<string> Handle(CheckStatusQuery query, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult($"Service '{query.ServiceName}' is Active");
    }
}

public sealed record TriggerAuditCommand(string Action) : ICommand<bool>;

public sealed class TriggerAuditCommandHandler : ICommandHandler<TriggerAuditCommand, bool>
{
    public ValueTask<bool> Handle(TriggerAuditCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(true);
    }
}

/// <summary>
/// Level 9: Ecosystem Extensions (OpenTelemetry, Testing, ASP.NET Core, and Health Checks).
/// </summary>
public static class Demo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 9: ECOSYSTEM EXTENSIONS (TELEMETRY, TESTING & WEB)");
        Console.WriteLine("================================================================================");

        // 1. AddMediatorOpenTelemetry — DI registration (compiled code)
        Console.WriteLine("1. AddMediatorOpenTelemetry() — DI configuration in executable code:");
        var otServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        otServices.AddMediatorOpenTelemetry(options =>
        {
            options.ActivitySourceName = "EricksonLopez.Mediator.Showcase";
            options.EnrichActivity = (activity, req) =>
                activity.SetTag("custom.environment", "Production");
        });
        var otProvider = otServices.BuildServiceProvider();
        var otelOptions = otProvider.GetRequiredService<MediatorOpenTelemetryOptions>();
        Console.WriteLine($"   -> Configured ActivitySource: {otelOptions.ActivitySourceName}");
        Console.WriteLine($"   -> EnrichActivity callback registered: {otelOptions.EnrichActivity is not null}");
        Console.WriteLine();

        // 2. OpenTelemetryBehavior<TRequest,TResponse> — compiled class usage
        Console.WriteLine("2. OpenTelemetryBehavior<TRequest,TResponse> — using the class directly:");
        Console.WriteLine("   This behavior implements IPipelineBehavior<TRequest,TResponse>.");
        Console.WriteLine("   Registration pattern:");
        Console.WriteLine("   [assembly: UseGlobalBehavior(typeof(OpenTelemetryBehavior<,>), order: 0)]");
        Console.WriteLine("   Or via DI open-generic registration:");
        Console.WriteLine("   services.AddTransient(typeof(IPipelineBehavior<,>), typeof(OpenTelemetryBehavior<,>));");
        // Instantiate with options to verify both constructors compile
        var behaviorWithDefaults = new OpenTelemetryBehavior<CheckStatusQuery, string>();
        var behaviorWithOptions = new OpenTelemetryBehavior<CheckStatusQuery, string>(otelOptions);
        Console.WriteLine($"   -> Default constructor (uses ActivitySource 'EricksonLopez.Mediator'): activated={behaviorWithDefaults is not null}");
        Console.WriteLine($"   -> Options constructor (custom ActivitySource '{otelOptions.ActivitySourceName}'): activated={behaviorWithOptions is not null}");
        Console.WriteLine();

        // 3. ASP.NET Core Minimal APIs — MapCommand and MapQuery (both overloads in context)
        Console.WriteLine("3. Minimal API Routing — MapCommand<,> and MapQuery<,> compiled extension methods:");
        Console.WriteLine("   Overload 1 (default POST):  app.MapCommand<CreateUserCommand, UserResponse>(\"/api/users\")");
        Console.WriteLine("   Overload 2 (explicit method): app.MapCommand<CreateUserCommand, UserResponse>(\"/api/users\", \"PUT\")");
        Console.WriteLine("   Query (default GET):          app.MapQuery<GetUserQuery, UserResponse>(\"/api/users/{id}\")");
        Console.WriteLine("   -> MapCommand routes to ISender.SendCommand<TCommand,TResponse>()");
        Console.WriteLine("   -> MapQuery routes to ISender.SendQuery<TQuery,TResponse>()");
        Console.WriteLine("   -> Body binding via [FromBody] for commands; [AsParameters] for queries");
        Console.WriteLine();

        // 4. AOT-Friendly Unit Testing with FakeMediator (full demonstration)
        Console.WriteLine("4. FakeMediator — AOT unit testing without dynamic mocks:");
        var fake = new FakeMediator();

        // Sync overloads (covered in Level 9 baseline)
        fake.SetupCommand<TriggerAuditCommand, bool>(cmd => true);
        fake.SetupQuery<CheckStatusQuery, string>(q => $"Mocked: {q.ServiceName} OK");

        var auditRes = await fake.Send(new TriggerAuditCommand("UserLogin"), CancellationToken.None);
        var statusRes = await fake.Send(new CheckStatusQuery("AuthService"), CancellationToken.None);

        fake.ShouldHaveReceived<TriggerAuditCommand>(c => c.Action == "UserLogin");
        fake.ShouldHaveReceived<CheckStatusQuery>();
        Console.WriteLine($"   -> Fake Command Result: {auditRes}");
        Console.WriteLine($"   -> Fake Query Result: {statusRes}");
        Console.WriteLine($"   -> ReceivedRequests.Count = {fake.ReceivedRequests.Count}");
        Console.WriteLine($"   -> ReceivedCount<TriggerAuditCommand>() = {fake.ReceivedCount<TriggerAuditCommand>()}");
        Console.WriteLine();

        // 5. Health Checks Readiness & Liveness
        Console.WriteLine("5. Mediator Health Check (MediatorHealthCheck):");
        var fakeSender = fake;
        var healthCheck = new MediatorHealthCheck(fakeSender);
        var healthResult = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        Console.WriteLine($"   -> Health Status: {healthResult.Status} ({healthResult.Description})");
        Console.WriteLine();

        // 6. MediatorHealthCheck degraded state (null sender — simulates missing DI registration)
        Console.WriteLine("6. MediatorHealthCheck degraded state (no sender registered):");
        var degradedCheck = new MediatorHealthCheck(sender: null);
        var degradedContext = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("mediator", degradedCheck, HealthStatus.Degraded, null)
        };
        var degradedResult = await degradedCheck.CheckHealthAsync(degradedContext);
        Console.WriteLine($"   -> Degraded Status: {degradedResult.Status}");
        Console.WriteLine($"   -> Description: {degradedResult.Description}");

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}
