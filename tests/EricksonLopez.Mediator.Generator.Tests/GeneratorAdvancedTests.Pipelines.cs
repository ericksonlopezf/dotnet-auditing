// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

/// <summary>
/// Represents GeneratorAdvancedTests.
/// </summary>
public partial class GeneratorAdvancedTests
{

    /// <summary>
    /// Executes EmptyCompilation_DoesNotGenerateFiles.
    /// </summary>
    [Fact]
    public void EmptyCompilation_DoesNotGenerateFiles()
    {
        string source = @"
namespace TestApp
{
    /// <summary>
    /// Represents NormalClass.
    /// </summary>
    public class NormalClass { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        // Only the original syntax tree should be present
        Assert.Single(generatedSyntaxTrees);
    }
    /// <summary>
    /// Executes Dispatcher_GeneratesPipeline_WhenBehaviorsArePresent.
    /// </summary>
    [Fact]
    public void Dispatcher_GeneratesPipeline_WhenBehaviorsArePresent()
    {
        string source = @"

[assembly: EricksonLopez.Mediator.UseGlobalBehavior(typeof(TestApp.LoggingBehavior))]
[assembly: EricksonLopez.Mediator.UseGlobalBehavior(typeof(TestApp.ValidationBehavior), 2)]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(string))]

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    [UseBehavior(typeof(SpecificBehavior))]
    [UseBehavior(typeof(ClosedGenericBehavior<MyCommand>))]
    [System.Obsolete]
    public class MyCommand : ICommand<int> { }
    
    /// <summary>
    /// Represents MyCommandHandler.
    /// </summary>
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }

    /// <summary>
    /// Represents LoggingBehavior.
    /// </summary>
    public class LoggingBehavior : IPipelineBehavior<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(MyCommand req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }
    
    /// <summary>
    /// Represents ValidationBehavior.
    /// </summary>
    public class ValidationBehavior : IPipelineBehavior<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(MyCommand req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }

    /// <summary>
    /// Represents SpecificBehavior.
    /// </summary>
    public class SpecificBehavior : IPipelineBehavior<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(MyCommand req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }

    /// <summary>
    /// Represents ClosedGenericBehavior.
    /// </summary>
    public class ClosedGenericBehavior<TRequest> : IPipelineBehavior<TRequest, int> where TRequest : ICommand<int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(TRequest req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }
}
";
        var compilation = CreateCompilation(source);

        var compDiagnostics = compilation.GetDiagnostics();
        compDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Should generate structs for next
        dispatcherCode.Should().Contain("internal readonly struct MyCommandHandlerNext");
        dispatcherCode.Should().Contain("internal readonly struct MyCommandBehavior0Next");
        dispatcherCode.Should().Contain("internal readonly struct MyCommandBehavior1Next");

        // Should chain them in the switch case
        Assert.Contains("var handlerNext = new MyCommandHandlerNext(handler, req, cancellationToken);", dispatcherCode);
        dispatcherCode.Should().Contain("var b3 = _serviceProvider.GetRequiredService<global::TestApp.ValidationBehavior>();");
        dispatcherCode.Should().Contain("var b2 = _serviceProvider.GetRequiredService<global::TestApp.ClosedGenericBehavior<global::TestApp.MyCommand>>();");
        dispatcherCode.Should().Contain("var b1 = _serviceProvider.GetRequiredService<global::TestApp.SpecificBehavior>();");
        dispatcherCode.Should().Contain("var b0 = _serviceProvider.GetRequiredService<global::TestApp.LoggingBehavior>();");
    }

    /// <summary>
    /// Executes Dispatcher_GeneratesParallelNotification_WhenStrategyIsParallel.
    /// </summary>
    [Fact]
    public void Dispatcher_GeneratesParallelNotification_WhenStrategyIsParallel()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyEvent.
    /// </summary>
    [PublishStrategy(PublishStrategy.Parallel)]
    public class MyEvent : INotification { }
    
    /// <summary>
    /// Represents Handler1.
    /// </summary>
    public class Handler1 : INotificationHandler<MyEvent>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask Handle(MyEvent notification, CancellationToken ct) => default;
    }
    /// <summary>
    /// Represents Handler2.
    /// </summary>
    public class Handler2 : INotificationHandler<MyEvent>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask Handle(MyEvent notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("var tasks = new Task[2];");
        Assert.Contains("tasks[0] = _sp.GetRequiredService<global::TestApp.Handler1>().Handle(_n, _ct).AsTask();", dispatcherCode);
        Assert.Contains("tasks[1] = _sp.GetRequiredService<global::TestApp.Handler2>().Handle(_n, _ct).AsTask();", dispatcherCode);
        dispatcherCode.Should().Contain("await Task.WhenAll(tasks).ConfigureAwait(false);");
    }

    /// <summary>
    /// Executes Dispatcher_GeneratesSequentialNotification_WhenStrategyIsSequential.
    /// </summary>
    [Fact]
    public void Dispatcher_GeneratesSequentialNotification_WhenStrategyIsSequential()
    {
        string source = @"

namespace TestApp
{
    // Sequential by default or explicitly
    /// <summary>
    /// Represents MyEvent.
    /// </summary>
    [PublishStrategy(PublishStrategy.Sequential)]
    public class MyEvent : INotification { }
    
    /// <summary>
    /// Represents Handler1.
    /// </summary>
    public class Handler1 : INotificationHandler<MyEvent>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask Handle(MyEvent notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("await _sp.GetRequiredService<global::TestApp.Handler1>().Handle(_n, _ct).ConfigureAwait(false);", dispatcherCode);
        dispatcherCode.Should().NotContain("var tasks = new Task[");
    }

    /// <summary>
    /// Executes DependencyInjection_GeneratesCorrectRegistrations.
    /// </summary>
    [Fact]
    public void DependencyInjection_GeneratesCorrectRegistrations()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand1.
    /// </summary>
    public class MyCommand1 : ICommand<int> { }
    /// <summary>
    /// Represents MyCommand2.
    /// </summary>
    public class MyCommand2 : ICommand<int> { }
    /// <summary>
    /// Represents MyCommand3.
    /// </summary>
    public class MyCommand3 : ICommand<int> { }

    /// <summary>
    /// Represents TransientHandler.
    /// </summary>
    [ServiceLifetime(HandlerLifetime.Transient)]
    public class TransientHandler : ICommandHandler<MyCommand1, int> { public ValueTask<int> Handle(MyCommand1 command, CancellationToken ct) => default; }

    /// <summary>
    /// Represents ScopedHandler.
    /// </summary>
    [ServiceLifetime(HandlerLifetime.Scoped)]
    public class ScopedHandler : ICommandHandler<MyCommand2, int> { public ValueTask<int> Handle(MyCommand2 command, CancellationToken ct) => default; }

    /// <summary>
    /// Represents SingletonHandler.
    /// </summary>
    [ServiceLifetime(HandlerLifetime.Singleton)]
    public class SingletonHandler : ICommandHandler<MyCommand3, int> { public ValueTask<int> Handle(MyCommand3 command, CancellationToken ct) => default; }
    
    /// <summary>
    /// Represents SomeQuery.
    /// </summary>
    public class SomeQuery : IQuery<int> { }
    /// <summary>
    /// Represents QueryHandler.
    /// </summary>
    public class QueryHandler : IQueryHandler<SomeQuery, int> { public ValueTask<int> Handle(SomeQuery query, CancellationToken ct) => default; }
    
    /// <summary>
    /// Represents SomeEvent.
    /// </summary>
    public class SomeEvent : INotification { }
    /// <summary>
    /// Represents EventHandler.
    /// </summary>
    public class EventHandler : INotificationHandler<SomeEvent> { public ValueTask Handle(SomeEvent notification, CancellationToken ct) => default; }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var diCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        string expected = @"// <auto-generated/>
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Generated;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GeneratedMediatorExtensions
    {
        public static IServiceCollection AddEricksonLopezMediator(this IServiceCollection services)
        {
            services.AddSingleton<IMediator, GeneratedMediator>();
            services.AddSingleton<ISender>(sp => sp.GetRequiredService<IMediator>());
            services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<IMediator>());

            services.AddTransient<global::TestApp.TransientHandler>();
            services.AddScoped<global::TestApp.ScopedHandler>();
            services.AddSingleton<global::TestApp.SingletonHandler>();
            services.AddTransient<global::TestApp.QueryHandler>();
            services.AddTransient<global::TestApp.EventHandler>();

            return services;
        }
    }

}
";
        Assert.Equal(expected.Replace("\r\n", "\n"), diCode.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Executes Dispatcher_GeneratesCorrectStructure.
    /// </summary>
    [Fact]
    public void Dispatcher_GeneratesCorrectStructure()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<int> { }
    /// <summary>
    /// Represents MyCommandHandler.
    /// </summary>
    public class MyCommandHandler : ICommandHandler<MyCommand, int> { public ValueTask<int> Handle(MyCommand c, CancellationToken ct) => default; }
    
    /// <summary>
    /// Represents MySeqNotification.
    /// </summary>
    [PublishStrategy(PublishStrategy.Sequential)]
    public class MySeqNotification : INotification { }
    /// <summary>
    /// Represents MySeqNotificationHandler.
    /// </summary>
    public class MySeqNotificationHandler : INotificationHandler<MySeqNotification> { public ValueTask Handle(MySeqNotification n, CancellationToken ct) => default; }
    
    /// <summary>
    /// Represents MyParNotification.
    /// </summary>
    [PublishStrategy(PublishStrategy.Parallel)]
    public class MyParNotification : INotification { }
    /// <summary>
    /// Represents MyParNotificationHandler1.
    /// </summary>
    public class MyParNotificationHandler1 : INotificationHandler<MyParNotification> { public ValueTask Handle(MyParNotification n, CancellationToken ct) => default; }
    /// <summary>
    /// Represents MyParNotificationHandler2.
    /// </summary>
    public class MyParNotificationHandler2 : INotificationHandler<MyParNotification> { public ValueTask Handle(MyParNotification n, CancellationToken ct) => default; }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("internal readonly struct MyCommandHandlerNext : INext<int>");
        dispatcherCode.Should().Contain("public sealed class GeneratedMediator : IMediator");
        dispatcherCode.Should().Contain("switch (command)");
        dispatcherCode.Should().Contain("switch (notification)");
        dispatcherCode.Should().Contain("switch (request)");
        dispatcherCode.Should().Contain("internal readonly struct MySeqNotificationNotificationNext : INext");
        dispatcherCode.Should().Contain("internal readonly struct MyParNotificationNotificationNext : INext");
        Assert.Contains("await _sp.GetRequiredService<global::TestApp.MySeqNotificationHandler>().Handle(_n, _ct).ConfigureAwait(false);", dispatcherCode);
        Assert.Contains("tasks[0] = _sp.GetRequiredService<global::TestApp.MyParNotificationHandler1>().Handle(_n, _ct).AsTask();", dispatcherCode);
    }
}

