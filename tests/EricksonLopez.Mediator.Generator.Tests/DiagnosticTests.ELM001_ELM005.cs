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
    public void MultipleCommandHandlers_ProducesELM002()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public class MyCommandHandler1 : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }

    public class MyCommandHandler2 : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(2);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM002", DiagnosticSeverity.Error, "Multiple Handlers", "Multiple command handlers found for");
    }

    [Fact]
    public void MultipleQueryHandlers_ProducesELM003()
    {
        string source = @"

namespace TestApp
{
    public class MyQuery : IQuery<int> { }
    
    public class MyQueryHandler1 : IQueryHandler<MyQuery, int>
    {
        public ValueTask<int> Handle(MyQuery query, CancellationToken ct) => new(1);
    }

    public class MyQueryHandler2 : IQueryHandler<MyQuery, int>
    {
        public ValueTask<int> Handle(MyQuery query, CancellationToken ct) => new(2);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM003", DiagnosticSeverity.Error, "Multiple Handlers", "Multiple query handlers found for");
    }

    [Fact]
    public void MissingCommandHandlers_ProducesELM001()
    {
        string source = @"

namespace TestApp
{
    public class OrphanCommand : ICommand<int> { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM001", DiagnosticSeverity.Error, "Handler Not Found", "No handler found for");
    }

    [Fact]
    public void MissingQueryHandlers_ProducesELM001()
    {
        string source = @"

namespace TestApp
{
    public class OrphanQuery : IQuery<int> { }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM001", DiagnosticSeverity.Error, "Handler Not Found", "No handler found for");
    }

    [Fact]
    public void InvalidCommandHandlerSignature_ProducesELM004()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public class BadCommandHandler : ICommandHandler<MyCommand, int>
    {
        public int Handle(MyCommand command) => 42;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM004", DiagnosticSeverity.Error, "Invalid Handler Signature", "must have a method 'ValueTask<Int32> Handle(MyCommand command, CancellationToken cancellationToken)'");
    }

    [Fact]
    public void InvalidNotificationHandlerSignature_ProducesELM004()
    {
        string source = @"

namespace TestApp
{
    public class MyNotification : INotification { }
    
    public class BadNotificationHandler : INotificationHandler<MyNotification>
    {
        public void Handle(MyNotification command) { }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM004", DiagnosticSeverity.Error, "Invalid Handler Signature", "must have a method 'ValueTask Handle(MyNotification notification, CancellationToken cancellationToken)'");
    }

    [Fact]
    public void InvalidQueryHandlerSignature_ProducesELM004()
    {
        string source = @"

namespace TestApp
{
    public class MyQuery : IQuery<int> { }
    
    public class BadQueryHandler : IQueryHandler<MyQuery, int>
    {
        public int Handle(MyQuery query) => 42;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM004", DiagnosticSeverity.Error, "Invalid Handler Signature", "must have a method 'ValueTask<Int32> Handle(MyQuery command, CancellationToken cancellationToken)'");
    }

    [Fact]
    public void InvalidBehaviorSignature_ProducesELM004()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }

    [UseBehavior(typeof(BadBehavior))]
    public class MyBadCommand : ICommand<int> { }

    public class MyBadCommandHandler : ICommandHandler<MyBadCommand, int>
    {
        public ValueTask<int> Handle(MyBadCommand command, CancellationToken ct) => new(1);
    }

    public class BadBehavior : IPipelineBehavior<MyBadCommand, int>
    {
        // Missing proper Next delegate handle method
        public void Handle() { }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM004", DiagnosticSeverity.Error, "Invalid Behavior Signature", "must have a method 'ValueTask<TResponse> Handle");
    }

    [Fact]
    public void OpenGenericQueryHandler_ProducesELM005()
    {
        string source = @"

namespace TestApp
{
    public class MyQuery<T> : IQuery<T> { }
    
    public class MyQueryHandler<T> : IQueryHandler<MyQuery<T>, T>
    {
        public ValueTask<T> Handle(MyQuery<T> query, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM005", DiagnosticSeverity.Warning, "Open Generic Handler Not Supported", "is an open generic type and will NOT be registered");
    }

    [Fact]
    public void OpenGenericNotificationHandler_ProducesELM005()
    {
        string source = @"

namespace TestApp
{
    public class MyEvent<T> : INotification { }
    
    public class MyEventHandler<T> : INotificationHandler<MyEvent<T>>
    {
        public ValueTask Handle(MyEvent<T> notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM005", DiagnosticSeverity.Warning, "Open Generic Handler Not Supported", "is an open generic type and will NOT be registered");
    }

    [Fact]
    public void OpenGenericStreamHandler_ProducesELM005()
    {
        string source = @"

namespace TestApp
{
    public class MyStreamRequest<T> : IStreamRequest<T> { }
    
    public class MyStreamHandler<T> : IStreamRequestHandler<MyStreamRequest<T>, T>
    {
        public IAsyncEnumerable<T> Handle(MyStreamRequest<T> request, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM005", DiagnosticSeverity.Warning, "Open Generic Handler Not Supported", "is an open generic type and will NOT be registered");
    }

    [Fact]
    public void OpenGenericCommandHandler_ProducesELM005()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand<T> : ICommand<T> { }
    
    public class MyCommandHandler<T> : ICommandHandler<MyCommand<T>, T>
    {
        public ValueTask<T> Handle(MyCommand<T> command, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        AssertDiagnostic(diagnostics, "ELM005", DiagnosticSeverity.Warning, "Open Generic Handler Not Supported", "is an open generic type and will NOT be registered");
    }

    [Fact]
    public void MissingCommandWithMultipleInterfaces_ProducesELM001()
    {
        string source = @"

namespace TestApp
{
    public class OrphanCommand : ICommand<int>, IDisposable 
    { 
        public void Dispose() {}
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        AssertDiagnostic(diagnostics, "ELM001", DiagnosticSeverity.Error, "Handler Not Found", "No handler found for");
    }

    [Fact]
    public void MissingQueryWithMultipleInterfaces_ProducesELM001()
    {
        string source = @"

namespace TestApp
{
    public class OrphanQuery : IQuery<int>, IDisposable 
    { 
        public void Dispose() {}
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        AssertDiagnostic(diagnostics, "ELM001", DiagnosticSeverity.Error, "Handler Not Found", "No handler found for");
    }
}
