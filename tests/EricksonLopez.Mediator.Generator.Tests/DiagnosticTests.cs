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
    private static Compilation CreateCompilation(string source) => RoslynTestHelper.CreateCompilation(source, "DiagnosticTestsComp");

    private static Diagnostic AssertDiagnostic(
        System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics,
        string id,
        DiagnosticSeverity expectedSeverity = DiagnosticSeverity.Error,
        string? expectedTitle = null,
        string? expectedMessageSubstring = null)
        => RoslynTestHelper.AssertDiagnostic(diagnostics, id, expectedSeverity, expectedTitle, expectedMessageSubstring);
}
