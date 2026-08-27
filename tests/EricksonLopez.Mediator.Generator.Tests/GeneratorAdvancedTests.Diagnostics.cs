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
    private static Compilation CreateCompilation(string source)
        => RoslynTestHelper.CreateCompilation(source, "CompAdvanced");

    /// <summary>
    /// Executes OpenGenericHandler_ProducesELM005.
    /// </summary>
    [Fact]
    public void OpenGenericHandler_ProducesELM005()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand<T> : ICommand<T> { }
    
    /// <summary>
    /// Represents MyCommandHandler.
    /// </summary>
    public class MyCommandHandler<T> : ICommandHandler<MyCommand<T>, T>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<T> Handle(MyCommand<T> command, CancellationToken cancellationToken)
        {
            return default;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM005" && d.GetMessage() == "Handler 'TestApp.MyCommandHandler<T>' is an open generic type and will NOT be registered by the source generator. Only concrete (closed) handler types are supported. Create a concrete handler that inherits from or wraps this generic handler if needed.");
    }

    /// <summary>
    /// Executes NotificationWithoutHandler_ProducesELM006.
    /// </summary>
    [Fact]
    public void NotificationWithoutHandler_ProducesELM006()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyNotification.
    /// </summary>
    public class MyNotification : INotification { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM006" && d.GetMessage() == "No handler found for notification TestApp.MyNotification. It will be ignored when published.");
    }

    /// <summary>
    /// Executes InvalidNotificationHandler_ProducesELM004.
    /// </summary>
    [Fact]
    public void InvalidNotificationHandler_ProducesELM004()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyNotification.
    /// </summary>
    public class MyNotification : INotification { }
    
    /// <summary>
    /// Represents BadNotificationHandler.
    /// </summary>
    public class BadNotificationHandler : INotificationHandler<MyNotification>
    {
        // Missing CancellationToken parameter
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask Handle(MyNotification notification)
        {
            return default;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM004" && d.GetMessage() == "Notification handler TestApp.BadNotificationHandler must have a method 'ValueTask Handle(MyNotification notification, CancellationToken cancellationToken)'.");
    }

    /// <summary>
    /// Executes InvalidBehaviorSignature_ProducesELM004.
    /// </summary>
    [Fact]
    public void InvalidBehaviorSignature_ProducesELM004()
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
    /// Represents BadBehavior.
    /// </summary>
    [UseBehavior(typeof(BadBehavior), 1)]
    public class BadBehavior : IPipelineBehavior<MyCommand, int>
    {
        // Bad signature: 2 parameters instead of 3
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(MyCommand request, TNext next)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM004" && d.GetMessage() == "Behavior TestApp.BadBehavior must have a method 'ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<TResponse>'.");
    }

    /// <summary>
    /// Executes BehaviorOrderConflict_ProducesELM008.
    /// </summary>
    [Fact]
    public void BehaviorOrderConflict_ProducesELM008()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents Behavior1.
    /// </summary>
    public class Behavior1 : IPipelineBehavior<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(MyCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }
    
    /// <summary>
    /// Represents Behavior2.
    /// </summary>
    public class Behavior2 : IPipelineBehavior<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<int> Handle<TNext>(MyCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }

    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    [UseBehavior(typeof(Behavior1), 1)]
    [UseBehavior(typeof(Behavior2), 1)]
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

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM008" && d.GetMessage() == "Behaviors TestApp.Behavior1, TestApp.Behavior2 have the same order (1). The execution order between them is not deterministic.");
    }

    /// <summary>
    /// Executes AbstractHandler_IsSkipped.
    /// </summary>
    [Fact]
    public void AbstractHandler_IsSkipped()
    {
        string source = @"

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<int> { }
    
    /// <summary>
    /// Represents AbstractCommandHandler.
    /// </summary>
    public abstract class AbstractCommandHandler : ICommandHandler<MyCommand, int>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public abstract ValueTask<int> Handle(MyCommand command, CancellationToken cancellationToken);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Should produce ELM001 because the abstract handler is ignored
        Assert.Contains(diagnostics, d => d.Id == "ELM001" && d.GetMessage() == "No handler found for TestApp.MyCommand");
    }
}

