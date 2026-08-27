// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class Phase2Tests
{
    private static Compilation CreateCompilation(string source)
        => RoslynTestHelper.CreateCompilation(source, "Phase2TestsComp");

    [Fact]
    public void Dispatcher_GeneratesCodeForExactlyOneBehavior()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestApp
{
    public class Behavior1 : IPipelineBehavior<MyCommand, int>
    {
        public ValueTask<int> Handle<TNext>(MyCommand request, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }

    [UseBehavior(typeof(Behavior1))]
    public class MyCommand : ICommand<int> { }
    
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var trees = outComp.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Exactly one behavior means the code uses `handlerNext` directly in `behavior.Handle(req, handlerNext, cancellationToken)`
        Assert.Contains("var result = b0.Handle(req, handlerNext, cancellationToken);", dispatcherCode);
    }

    [Fact]
    public void Dispatcher_GeneratesCodeForZeroBehaviors()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var trees = outComp.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("var result = handler.Handle(req, cancellationToken);", dispatcherCode);
    }

    [Fact]
    public void Dispatcher_GeneratesCodeForStreamHandlers()
    {
        string source = @"
using System.Runtime.CompilerServices;

namespace TestApp
{
    public class MyStreamRequest : IStreamRequest<int> { }
    
    public class MyStreamHandler : IStreamRequestHandler<MyStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(MyStreamRequest request, [EnumeratorCancellation] CancellationToken ct) 
        {
            await Task.Yield();
            yield return 42;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var trees = outComp.SyntaxTrees.ToList();
        var dispatcherCode = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        // Verify the stream handler switch statement is generated
        Assert.Contains("case global::TestApp.MyStreamRequest req:", dispatcherCode);
        Assert.Contains("var result = handler.Handle(req, cancellationToken);", dispatcherCode);
        Assert.Contains("Unsafe.As<IAsyncEnumerable<int>, IAsyncEnumerable<TResponse>>(ref result);", dispatcherCode);
    }
}




