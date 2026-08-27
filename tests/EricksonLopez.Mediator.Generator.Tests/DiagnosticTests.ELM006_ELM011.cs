// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public partial class DiagnosticTests
{

    [Fact]
    public void BehaviorOrderConflict_ProducesELM008()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }

    [UseBehavior(typeof(BehaviorA), 1)]
    [UseBehavior(typeof(BehaviorB), 1)]
    public class ConflictCommand : ICommand<int> { }

    public class ConflictCommandHandler : ICommandHandler<ConflictCommand, int>
    {
        public ValueTask<int> Handle(ConflictCommand command, CancellationToken ct) => new(1);
    }

    public class BehaviorA : IPipelineBehavior<ConflictCommand, int>
    {
        public ValueTask<int> Handle<TNext>(ConflictCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }

    public class BehaviorB : IPipelineBehavior<ConflictCommand, int>
    {
        public ValueTask<int> Handle<TNext>(ConflictCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM008", DiagnosticSeverity.Warning, "Behavior Order Conflict", "have the same order (1)");
    }

    [Fact]
    public void UnsupportedOpenGenericBehavior_ProducesELM007()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }

    [UseBehavior(typeof(BadBehavior<,,>))]
    public class MyOtherCommand : ICommand<int> { }

    public class MyOtherCommandHandler : ICommandHandler<MyOtherCommand, int>
    {
        public ValueTask<int> Handle(MyOtherCommand command, CancellationToken ct) => new(1);
    }

    // Has 3 generic parameters instead of 2
    public class BadBehavior<TRequest, TResponse, TExtra> : IPipelineBehavior<TRequest, TResponse>
    {
        public ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<TResponse> => next.InvokeAsync();
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM007", DiagnosticSeverity.Error, "Unsupported Open Generic Behavior", "has 3 type parameters. Only 2 are supported (TRequest, TResponse).");
    }

    [Fact]
    public void UnsupportedOpenGenericNotificationBehavior_ProducesELM007()
    {
        string source = @"

namespace TestApp
{
    public class MyNotification : INotification { }
    
    public class MyNotificationHandler : INotificationHandler<MyNotification>
    {
        public ValueTask Handle(MyNotification notification, CancellationToken ct) => default;
    }

    [UseBehavior(typeof(BadNotificationBehavior<,>))]
    public class MyOtherNotification : INotification { }

    public class MyOtherNotificationHandler : INotificationHandler<MyOtherNotification>
    {
        public ValueTask Handle(MyOtherNotification notification, CancellationToken ct) => default;
    }

    // Has 2 generic parameters instead of 1
    public class BadNotificationBehavior<TNotification, TExtra> : INotificationBehavior<TNotification>
        where TNotification : INotification
    {
        public ValueTask Handle<TNext>(TNotification notification, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INotificationNext => next.InvokeAsync();
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM007", DiagnosticSeverity.Error, "Unsupported Open Generic Behavior", "has 2 type parameters. Only 1 is supported (TNotification) for notifications.");
    }

    [Fact]
    public void MultipleStreamHandlers_ProducesELM010()
    {
        string source = @"
using System.Collections.Generic;

namespace TestApp
{
    public class MyStreamRequest : IStreamRequest<int> { }
    
    public class MyStreamHandler1 : IStreamRequestHandler<MyStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(MyStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) 
        {
            yield return 1;
        }
    }

    public class MyStreamHandler2 : IStreamRequestHandler<MyStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(MyStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) 
        {
            yield return 2;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM010", DiagnosticSeverity.Error, "Multiple Handlers", "Multiple stream handlers found for");
    }

    [Fact]
    public void MissingStreamHandlers_ProducesELM009()
    {
        string source = @"

namespace TestApp
{
    public class OrphanStreamRequest : IStreamRequest<int> { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM009", DiagnosticSeverity.Error, "Stream Handler Not Found", "No stream handler found for");
    }

    [Fact]
    public void InvalidStreamHandlerSignature_ProducesELM011()
    {
        string source = @"

namespace TestApp
{
    public class MyStreamRequest : IStreamRequest<int> { }
    
    public class BadStreamHandler : IStreamRequestHandler<MyStreamRequest, int>
    {
        // Missing CancellationToken parameter
        public IAsyncEnumerable<int> Handle(MyStreamRequest request) 
        {
            yield break;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM011", DiagnosticSeverity.Error, "Invalid Stream Handler Signature", "must have a method 'IAsyncEnumerable<Int32> Handle(MyStreamRequest request, CancellationToken cancellationToken)'");
    }

    [Fact]
    public void MissingNotificationWithMultipleInterfaces_ProducesELM006Warning()
    {
        string source = @"

namespace TestApp
{
    public class OrphanNotification : INotification, IDisposable 
    { 
        public void Dispose() {}
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        AssertDiagnostic(diagnostics, "ELM006", DiagnosticSeverity.Warning, "Notification Handler Not Found", "No handler found for notification");
    }

    [Fact]
    public void MissingStreamRequestWithMultipleInterfaces_ProducesELM009()
    {
        string source = @"

namespace TestApp
{
    public class OrphanStreamRequest : IStreamRequest<int>, IDisposable 
    { 
        public void Dispose() {}
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        AssertDiagnostic(diagnostics, "ELM009", DiagnosticSeverity.Error, "Stream Handler Not Found", "No stream handler found for");
    }

    [Fact]
    public void NonBehaviorClassInUseBehavior_IsIgnoredByCloseBehaviorType()
    {
        string source = @"
namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }

    public class PlainClassNotABehavior { }

    [UseBehavior(typeof(PlainClassNotABehavior))]
    public class DecoratedCommand : ICommand<int> { }

    public class DecoratedCommandHandler : ICommandHandler<DecoratedCommand, int>
    {
        public ValueTask<int> Handle(DecoratedCommand command, CancellationToken ct) => new(2);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
