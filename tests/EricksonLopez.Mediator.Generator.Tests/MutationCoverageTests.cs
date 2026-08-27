// Copyright © Erickson Lopez. MIT License.
using Microsoft.CodeAnalysis;

namespace EricksonLopez.Mediator.Generator.Tests;

public partial class MutationCoverageTests
{
    private static Compilation CreateCompilation(string source, string assemblyName = "TestAssembly") =>
        RoslynTestHelper.CreateCompilation(source, assemblyName);
}
