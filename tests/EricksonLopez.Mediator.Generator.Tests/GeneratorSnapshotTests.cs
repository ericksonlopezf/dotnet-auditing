// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

/// <summary>
/// Represents GeneratorSnapshotTests.
/// </summary>
public class GeneratorSnapshotTests
{
    private static Compilation CreateCompilation(string source) => RoslynTestHelper.CreateCompilation(source, "SnapshotTestsComp");

    /// <summary>
    /// Executes ValidHandler_GeneratesCodeWithoutErrors.
    /// </summary>
    [Fact]
    public void ValidHandler_GeneratesCodeWithoutErrors()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<int> { }
    
    /// <summary>
    /// Represents MyCommandHandler.
    /// </summary>
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyCommand command, CancellationToken cancellationToken)
        {
            return new ValueTask<int>(42);
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        Assert.Equal(3, generatedSyntaxTrees.Count); // 1 original + 2 generated

        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();
        var diCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.Contains("case global::TestApp.MyCommand req:", dispatcherCode);
        Assert.Contains("services.AddTransient<global::TestApp.MyCommandHandler>();", diCode);
    }

    /// <summary>
    /// Executes MissingHandler_ProducesELM001.
    /// </summary>
    [Fact]
    public void MissingHandler_ProducesELM001()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<int> { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ELM001", diagnostic.Id);
        Assert.Equal("No handler found for TestApp.MyCommand", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    /// <summary>
    /// Executes MultipleHandlers_ProducesELM002.
    /// </summary>
    [Fact]
    public void MultipleHandlers_ProducesELM002()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<int> { }
    
    /// <summary>
    /// Represents Handler1.
    /// </summary>
    public class Handler1 : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }
    
    /// <summary>
    /// Represents Handler2.
    /// </summary>
    public class Handler2 : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(2);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ELM002", diagnostic.Id);
        Assert.Equal("Multiple command handlers found for TestApp.MyCommand", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    /// <summary>
    /// Executes InvalidHandlerSignature_ProducesELM004.
    /// </summary>
    [Fact]
    public void InvalidHandlerSignature_ProducesELM004()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<int> { }
    
    /// <summary>
    /// Represents BadHandler.
    /// </summary>
    public class BadHandler : ICommandHandler<MyCommand, int>
    {
        // Wrong return type
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public Task<int> Handle(MyCommand command, CancellationToken cancellationToken)
        {
            return Task.FromResult(42);
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM004" && d.GetMessage() == "Handler TestApp.BadHandler must have a method 'ValueTask<Int32> Handle(MyCommand command, CancellationToken cancellationToken)'.");
        Assert.Contains(diagnostics, d => d.Id == "ELM001" && d.GetMessage() == "No handler found for TestApp.MyCommand");
    }

    // ─── Fase 1 — Tests faltantes ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that a query without a handler produces an ELM001 diagnostic with a valid location.
    /// </summary>
    [Fact]
    public void MissingQueryHandler_ProducesELM001WithLocation()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyQuery.
    /// </summary>
    public class MyQuery : IQuery<int> { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ELM001", diagnostic.Id);
        Assert.Equal("No handler found for TestApp.MyQuery", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    /// <summary>
    /// Verifies that two query handlers for the same query produce ELM003 with a valid location.
    /// </summary>
    [Fact]
    public void MultipleQueryHandlers_ProducesELM003WithLocation()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyQuery.
    /// </summary>
    public class MyQuery : IQuery<int> { }

    /// <summary>
    /// Represents Handler1.
    /// </summary>
    public class Handler1 : IQueryHandler<MyQuery, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyQuery query, CancellationToken ct) => new(1);
    }

    /// <summary>
    /// Represents Handler2.
    /// </summary>
    public class Handler2 : IQueryHandler<MyQuery, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyQuery query, CancellationToken ct) => new(2);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ELM003", diagnostic.Id);
        Assert.Equal("Multiple query handlers found for TestApp.MyQuery", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    /// <summary>
    /// Verifies that an open generic behavior with 3 type parameters produces ELM007.
    /// </summary>
    [Fact]
    public void OpenGenericBehaviorWithThreeTypeParams_ProducesELM007()
    {
        string source = @"

[assembly: UseGlobalBehavior(typeof(TestApp.BadBehavior<,,>))]

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
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
    /// Represents BadBehavior.
    /// </summary>
    public class BadBehavior<TRequest, TResponse, TExtra> : IPipelineBehavior<TRequest, TResponse>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<TResponse>
            => next.InvokeAsync();
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM007" && d.GetMessage() == "Behavior TestApp.BadBehavior<,,> has 3 type parameters. Only 2 are supported (TRequest, TResponse).");
    }

    /// <summary>
    /// Snapshot: verifies the GeneratedMediator.g.cs contains the expected dispatcher structure
    /// for a command and a query handler.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedMediator_ContainsExpectedStructure()
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
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }

    /// <summary>
    /// Represents MyQuery.
    /// </summary>
    public class MyQuery : IQuery<string> { }
    /// <summary>
    /// Represents MyQueryHandler.
    /// </summary>
    public class MyQueryHandler : IQueryHandler<MyQuery, string>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<string> Handle(MyQuery query, CancellationToken ct) => new(""result"");
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var trees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Class structure
        Assert.Contains("public sealed class GeneratedMediator : IMediator", dispatcherCode);
        Assert.Contains("public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command", dispatcherCode);
        Assert.Contains("public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query", dispatcherCode);
        Assert.Contains("public async ValueTask Publish<TNotification>", dispatcherCode);

        // Command dispatch case
        Assert.Contains("case global::TestApp.MyCommand req:", dispatcherCode);
        Assert.Contains("global::TestApp.MyCommandHandler", dispatcherCode);

        // Query dispatch case
        Assert.Contains("case global::TestApp.MyQuery req:", dispatcherCode);
        Assert.Contains("global::TestApp.MyQueryHandler", dispatcherCode);

        // Handler struct (zero-allocation pipeline)
        Assert.Contains("MyCommandHandlerNext", dispatcherCode);
        Assert.Contains("MyQueryHandlerNext", dispatcherCode);
    }

    /// <summary>
    /// Snapshot: verifies the GeneratedMediatorExtensions.g.cs contains correct DI registrations.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedMediatorExtensions_ContainsDIRegistrations()
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
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var trees = outputCompilation.SyntaxTrees.ToList();
        var diCode = trees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        // Extension method structure
        Assert.Contains("AddEricksonLopezMediator", diCode);
        Assert.Contains("IServiceCollection", diCode);

        // Mediator registration
        Assert.Contains("GeneratedMediator", diCode);

        // Handler registration with correct lifetime (Transient by default)
        Assert.Contains("services.AddTransient<global::TestApp.MyCommandHandler>();", diCode);
    }

    /// <summary>
    /// Snapshot: verifies the GeneratedMediator.g.cs contains the correct nested struct chain
    /// when multiple ordered behaviors are configured.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedMediator_WithOrderedBehaviors_GeneratesNestedStructChain()
    {
        string source = @"
namespace TestApp
{
    public class LogBehavior : IPipelineBehavior<MyOrderedCommand, string>
    {
        public ValueTask<string> Handle<TNext>(MyOrderedCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<string> => next.InvokeAsync();
    }

    public class AuthBehavior : IPipelineBehavior<MyOrderedCommand, string>
    {
        public ValueTask<string> Handle<TNext>(MyOrderedCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<string> => next.InvokeAsync();
    }

    [UseBehavior(typeof(AuthBehavior), 1)]
    [UseBehavior(typeof(LogBehavior), 2)]
    public class MyOrderedCommand : ICommand<string> { }

    public class MyOrderedCommandHandler : ICommandHandler<MyOrderedCommand, string>
    {
        public ValueTask<string> Handle(MyOrderedCommand command, CancellationToken ct) => new(""done"");
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var trees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Check struct pipeline nesting
        Assert.Contains("MyOrderedCommandHandlerNext", dispatcherCode);
        Assert.Contains("global::TestApp.AuthBehavior", dispatcherCode);
        Assert.Contains("global::TestApp.LogBehavior", dispatcherCode);
    }

    /// <summary>
    /// Snapshot: verifies the GeneratedMediator.g.cs contains CreateStream dispatch for IStreamRequest.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedMediator_WithStreamRequest_GeneratesStreamDispatch()
    {
        string source = @"
using System.Collections.Generic;

namespace TestApp
{
    public class MyStreamRequest : IStreamRequest<int> { }

    public class MyStreamHandler : IStreamRequestHandler<MyStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(MyStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return 1;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var trees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Check streaming method and case
        Assert.Contains("CreateStream<TResponse>(IStreamRequest<TResponse> request", dispatcherCode);
        Assert.Contains("case global::TestApp.MyStreamRequest req:", dispatcherCode);
        Assert.Contains("global::TestApp.MyStreamHandler", dispatcherCode);
    }

    /// <summary>
    /// Snapshot: verifies the GeneratedMediator.g.cs contains parallel execution for notifications with Parallel strategy.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedMediator_WithParallelNotification_GeneratesParallelDispatch()
    {
        string source = @"
namespace TestApp
{
    [PublishStrategy(PublishStrategy.Parallel)]
    public class OrderCreatedEvent : INotification { }

    public class HandlerA : INotificationHandler<OrderCreatedEvent>
    {
        public ValueTask Handle(OrderCreatedEvent notification, CancellationToken ct) => default;
    }

    public class HandlerB : INotificationHandler<OrderCreatedEvent>
    {
        public ValueTask Handle(OrderCreatedEvent notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var trees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Check parallel publishing dispatch
        Assert.Contains("case global::TestApp.OrderCreatedEvent n:", dispatcherCode);
        Assert.Contains("Task.WhenAll", dispatcherCode);
    }
}





