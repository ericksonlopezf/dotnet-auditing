// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class EdgeCaseTests
{
    private static Compilation CreateCompilation(string source)
        => RoslynTestHelper.CreateCompilation(source, "EdgeCaseTestsComp");

    [Fact]
    public void Handler_WithMultipleInterfaces_IsRegisteredCorrectly()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestApp
{
    public class MyCommand1 : ICommand<int> { }
    public class MyCommand2 : ICommand<string> { }
    
    public class MultiHandler : 
        ICommandHandler<MyCommand1, int>,
        ICommandHandler<MyCommand2, string>
    {
        public ValueTask<int> Handle(MyCommand1 command, CancellationToken ct) => new(1);
        public ValueTask<string> Handle(MyCommand2 command, CancellationToken ct) => new(""test"");
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void StructHandler_IsRegisteredCorrectly()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public struct MyStructHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void InvalidServiceLifetime_DefaultsToTransient()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    // 99 is invalid
    [ServiceLifetime((HandlerLifetime)99)]
    public class MyHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var trees = outComp.SyntaxTrees.ToList();
        var diCode = trees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();
        Assert.Contains("services.AddTransient<global::TestApp.MyHandler>();", diCode);
    }

    [Fact]
    public void InvalidPublishStrategy_DefaultsToSequential()
    {
        string source = @"

namespace TestApp
{
    // 99 is invalid
    [PublishStrategy((PublishStrategy)99)]
    public class MyEvent : INotification { }
    
    public class MyHandler : INotificationHandler<MyEvent>
    {
        public ValueTask Handle(MyEvent notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var trees = outComp.SyntaxTrees.ToList();
        var code = trees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();
        Assert.Contains("await _sp.GetRequiredService<global::TestApp.MyHandler>().Handle(_n, _ct).ConfigureAwait(false);", code);
    }

    [Fact]
    public void DiscoverHandlersAttribute_OnExternalAssembly_CollectsTypes()
    {
        // This is tricky to test since we need two compilations.
        // But we can simulate by applying it to a type in the same assembly.
        string source = @"

[assembly: DiscoverHandlers(typeof(TestApp.Marker))]

namespace TestApp
{
    public class Marker { }

    public class MyCommand : ICommand<int> { }
    
    public class MyHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void NestedClasses_AreDiscovered()
    {
        string source = @"

namespace TestApp
{
    public class OuterClass
    {
        public class MyCommand : ICommand<int> { }
        
        public class NestedHandler : ICommandHandler<MyCommand, int>
        {
            public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
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
        var diCode = trees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();
        Assert.Contains("services.AddTransient<global::TestApp.OuterClass.NestedHandler>();", diCode);
    }

    [Fact]
    public void ForeignInterface_IsIgnored()
    {
        string source = @"

namespace ForeignLib
{
    public interface ICommandHandler<TReq, TRes> { }
}

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public class MyHandler : IDisposable, ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
        public void Dispose() {}
    }

    public class MyForeignHandler : ForeignLib.ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(2);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics);

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.DoesNotContain("MyForeignHandler", diCode);
    }

    [Fact]
    public void AbstractHandler_IsIgnored()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public abstract class AbstractCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Ensure no diagnostics and generated code does not contain AbstractCommandHandler
        Assert.Contains(diagnostics, d => d.Id == "ELM001"); var syntaxTrees = outputCompilation.SyntaxTrees.ToList();
        Assert.Equal(3, syntaxTrees.Count); // Original + Dispatcher + DI

        var generatedDi = syntaxTrees[2].ToString();
        Assert.DoesNotContain("AbstractCommandHandler", generatedDi);
    }

    [Fact]
    public void DiscoverHandlersAttribute_RegistersExternalHandlers()
    {
        string externalSource = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace ExternalApp
{
    public class ExternalCommand : ICommand<int> { }
    
    public class ExternalCommandHandler : ICommandHandler<ExternalCommand, int>
    {
        public ValueTask<int> Handle(ExternalCommand command, CancellationToken ct) => new(42);
    }
    
    public class ExternalMarker { }
}";
        var externalCompilation = CSharpCompilation.Create("ExternalApp",
            new[] { CSharpSyntaxTree.ParseText(externalSource) },
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Append(MetadataReference.CreateFromFile(typeof(IMediator).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new System.IO.MemoryStream();
        var result = externalCompilation.Emit(ms);
        Assert.True(result.Success);
        var externalAssemblyRef = MetadataReference.CreateFromStream(new System.IO.MemoryStream(ms.ToArray()));

        string mainSource = @"
using EricksonLopez.Mediator;
using ExternalApp;

[assembly: DiscoverHandlers(typeof(ExternalMarker))]

namespace MainApp
{
}
";
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Append(MetadataReference.CreateFromFile(typeof(IMediator).Assembly.Location))
            .Append(externalAssemblyRef)
            .ToList();

        var compilation = CSharpCompilation.Create("MainApp",
            new[] { CSharpSyntaxTree.ParseText(mainSource) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        Assert.Empty(diagnostics);
        var syntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var generatedDi = syntaxTrees[2].ToString();
        Assert.Contains("ExternalCommandHandler", generatedDi);
    }
}







