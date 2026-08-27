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
    /// Executes DiscoverHandlersAttribute_DiscoversHandlersInExternalAssembly.
    /// </summary>
    [Fact]
    public void DiscoverHandlersAttribute_DiscoversHandlersInExternalAssembly()
    {
        // 1. Create a "library" compilation representing the external assembly
        string librarySource = @"

namespace ExternalLibrary
{
    public class ExternalCommand : ICommand<int> { }

    public class ExternalCommandHandler : ICommandHandler<ExternalCommand, int>
    {
        public ValueTask<int> Handle(ExternalCommand command, CancellationToken ct) => new(42);
    }

    public class OuterContainer
    {
        public class NestedExternalCommand : ICommand<string> { }

        public class NestedExternalCommandHandler : ICommandHandler<NestedExternalCommand, string>
        {
            public ValueTask<string> Handle(NestedExternalCommand command, CancellationToken ct) => new(""nested"");
        }
    }
}
";
        var libraryCompilation = CreateCompilation(librarySource);
        using var memoryStream = new System.IO.MemoryStream();
        var emitResult = libraryCompilation.Emit(memoryStream);
        emitResult.Success.Should().BeTrue();
        memoryStream.Position = 0;
        var externalReference = MetadataReference.CreateFromStream(memoryStream);

        // 2. Create the main app compilation that references the library and uses [DiscoverHandlers]
        // Including duplicate assembly marker to test visited assemblies deduplication
        string mainSource = @"
using ExternalLibrary;

[assembly: DiscoverHandlers(typeof(ExternalCommand))]
[assembly: DiscoverHandlers(typeof(ExternalCommandHandler))]

namespace MainApp
{
    // The main app is empty of handlers, it just triggers the discovery
    public class Program { }
}
";
        var compilation = RoslynTestHelper.CreateCompilation(mainSource, "MainApp", new[] { externalReference });
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();

        // Assert that the generator discovered the external handler and nested external handler
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();
        dispatcherCode.Should().Contain("global::ExternalLibrary.ExternalCommand req:");
        dispatcherCode.Should().Contain("var handler = _serviceProvider.GetRequiredService<global::ExternalLibrary.ExternalCommandHandler>();");
        dispatcherCode.Should().Contain("global::ExternalLibrary.OuterContainer.NestedExternalCommand req:");
        dispatcherCode.Should().Contain("var handler = _serviceProvider.GetRequiredService<global::ExternalLibrary.OuterContainer.NestedExternalCommandHandler>();");

        var diCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();
        diCode.Should().Contain("services.AddTransient<global::ExternalLibrary.ExternalCommandHandler>();");
        diCode.Should().Contain("services.AddTransient<global::ExternalLibrary.OuterContainer.NestedExternalCommandHandler>();");
    }
}

