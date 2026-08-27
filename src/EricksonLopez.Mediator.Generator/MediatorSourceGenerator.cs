// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Generates compile-time dispatchers and dependency injection registrations for mediator handlers and behaviors.
/// </summary>
[Generator]
public class MediatorSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Find all type declarations that have a base list (potential handlers/behaviors).
        // This is the "hot" predicate — it runs on every keystroke and should be as fast as possible.
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsTypeWithInterfaces(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Step 2: Collect all relevant symbols into a single equatable array.
        // The EquatableArray<INamedTypeSymbol> uses SymbolEqualityComparer so the cache
        // only invalidates when the set of relevant types actually changes.
        var collectedTypes = typeDeclarations.Collect()
            .Select(static (arr, _) =>
                new EquatableArray<INamedTypeSymbol>(
                    arr.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                       .OfType<INamedTypeSymbol>()
                       .ToArray()));

        // Step 3: Combine with compilation only at this point.
        // The compilation is needed for global behavior attributes (assembly-level).
        var compilationAndTypes = context.CompilationProvider.Combine(collectedTypes);

        context.RegisterSourceOutput(compilationAndTypes,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsTypeWithInterfaces(SyntaxNode node)
    {
        return node is TypeDeclarationSyntax { BaseList.Types.Count: not 0 };
    }

    private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var namedTypeSymbol = context.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)context.Node) as INamedTypeSymbol;
        return namedTypeSymbol?.AllInterfaces.Any(i => i.ContainingNamespace.ToDisplayString() == "EricksonLopez.Mediator") == true
            ? namedTypeSymbol
            : null;
    }

    private static void Execute(Compilation compilation, EquatableArray<INamedTypeSymbol> types, SourceProductionContext context)
    {
        var hasDiscoverHandlers = compilation.Assembly.GetAttributes().Any(a => GetAttributeClassName(a) == "DiscoverHandlersAttribute");

        if (types.Count == 0 && !hasDiscoverHandlers)
        {
            return;
        }

        var model = MediatorModelBuilder.Build(compilation, types.ToList(), context);

        var dispatcherSource = DispatcherGenerator.Generate(model);
        context.AddSource("GeneratedMediator.g.cs", SourceText.From(dispatcherSource, Encoding.UTF8));

        var diSource = DependencyInjectionGenerator.Generate(model);
        context.AddSource("GeneratedMediatorExtensions.g.cs", SourceText.From(diSource, Encoding.UTF8));
    }

    // Excluded from coverage: AttributeData instances produced by Roslyn
    // have a non-null AttributeClass under the supported compilation model.
    // The null branch is defensive against an invalid/inconsistent AST state.
    [ExcludeFromCodeCoverage]
    private static string? GetAttributeClassName(AttributeData attribute)
        => attribute.AttributeClass?.Name;

    // Excluded from coverage: A TypeDeclarationSyntax from a valid compilation
    // will yield an INamedTypeSymbol. Defensive against invalid ASTs.
    [ExcludeFromCodeCoverage]
    private static INamedTypeSymbol? GetNamedTypeSymbol(SemanticModel semanticModel, TypeDeclarationSyntax typeDeclaration)
    {
        return semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
    }
}


