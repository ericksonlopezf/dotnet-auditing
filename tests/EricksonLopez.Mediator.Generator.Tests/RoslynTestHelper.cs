// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EricksonLopez.Result;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

/// <summary>
/// Shared Roslyn compilation and generator execution helper for source generator tests.
/// </summary>
public static class RoslynTestHelper
{
    private static readonly Lazy<List<MetadataReference>> SharedReferences = new(() =>
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        refs.Add(MetadataReference.CreateFromFile(typeof(IMediator).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(EricksonLopez.Result.Result<>).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location));

        return refs;
    });

    /// <summary>
    /// Creates a CSharpCompilation for the given source code.
    /// </summary>
    public static Compilation CreateCompilation(
        string source,
        string assemblyName = "TestAssembly",
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var normalizedSource = source;
        if (!normalizedSource.Contains("using EricksonLopez.Mediator;"))
        {
            normalizedSource = "using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing EricksonLopez.Mediator;\n" + normalizedSource;
        }
        if (normalizedSource.Contains("Result<") && !normalizedSource.Contains("using EricksonLopez.Result;"))
        {
            normalizedSource = "using EricksonLopez.Result;\n" + normalizedSource;
        }

        var references = new List<MetadataReference>(SharedReferences.Value);
        if (additionalReferences != null)
        {
            references.AddRange(additionalReferences);
        }

        return CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(normalizedSource) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Runs an incremental generator and updates the compilation.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator(
        Compilation compilation,
        IIncrementalGenerator generator,
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out diagnostics);
        return driver.GetRunResult();
    }

    /// <summary>
    /// Runs the default MediatorSourceGenerator against the compilation.
    /// </summary>
    public static GeneratorDriverRunResult RunMediatorGenerator(
        Compilation compilation,
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        return RunGenerator(compilation, new MediatorSourceGenerator(), out outputCompilation, out diagnostics);
    }

    /// <summary>
    /// Asserts that a specific diagnostic was emitted and validates its attributes.
    /// </summary>
    public static Diagnostic AssertDiagnostic(
        ImmutableArray<Diagnostic> diagnostics,
        string expectedId,
        DiagnosticSeverity expectedSeverity = DiagnosticSeverity.Error,
        string? expectedTitle = null,
        string? expectedMessageSubstring = null)
    {
        var diagnostic = Assert.Single(diagnostics, d => d.Id == expectedId);
        var msg = diagnostic.GetMessage();
        Assert.False(string.IsNullOrWhiteSpace(msg), "Diagnostic message should not be empty");

        if (expectedMessageSubstring != null)
        {
            Assert.Contains(expectedMessageSubstring, msg);
        }

        var title = diagnostic.Descriptor.Title.ToString();
        Assert.False(string.IsNullOrWhiteSpace(title), "Diagnostic title should not be empty");

        if (expectedTitle != null)
        {
            Assert.Equal(expectedTitle, title);
        }

        Assert.Equal("Design", diagnostic.Descriptor.Category);
        Assert.Equal(expectedSeverity, diagnostic.Severity);
        Assert.Equal(expectedSeverity, diagnostic.Descriptor.DefaultSeverity);
        Assert.NotEqual(Location.None, diagnostic.Location);

        return diagnostic;
    }
}

