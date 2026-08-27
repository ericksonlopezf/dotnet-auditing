// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public partial class MutationCoverageTests
{

    [Fact]
    public void PartiallyOpenGenericHandler_ReportsELM005()
    {
        string source = @"

namespace TestApp
{
    public class SpecificCmd : ICommand<string> { }

    public class PartialGenericHandler<TResponse> : ICommandHandler<SpecificCmd, TResponse>
    {
        public ValueTask<TResponse> Handle(SpecificCmd cmd, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "ELM005");
    }

    [Fact]
    public void OpenGeneric_NonMediatorInterface_DoesNotTriggerELM005()
    {
        string source = @"

namespace TestApp
{
    public interface ICustomService<T> { }
    public class CustomService<T> : ICustomService<T> { }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "ELM005");
    }

    [Fact]
    public void OpenGeneric_MediatorHandler_TriggersELM005()
    {
        string source = @"

namespace TestApp
{
    public class OpenCmd<T> : ICommand<int> { }
    public class OpenCmdHandler<T> : ICommandHandler<OpenCmd<T>, int>
    {
        public ValueTask<int> Handle(OpenCmd<T> cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM005");
    }

    [Fact]
    public void OpenGeneric_ImplementingMultipleInterfaces_EmitsELM005()
    {
        string source = @"

namespace TestApp
{
    public class MultiOpenCmd<T> : ICommand<int> { }
    public class MultiOpenHandler<T> : IDisposable, ICommandHandler<MultiOpenCmd<T>, int>
    {
        public ValueTask<int> Handle(MultiOpenCmd<T> cmd, CancellationToken ct) => new(1);
        public void Dispose() { }
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM005");
    }

    [Fact]
    public void ValidateOpenGenericHandler_ClosedOrNonGenericType_DoesNotReportELM005()
    {
        string source = @"

namespace TestApp
{
    public class ClosedCmd : ICommand<int> { }
    public class ClosedCmdHandler : ICommandHandler<ClosedCmd, int>
    {
        public ValueTask<int> Handle(ClosedCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "ELM005");
    }
}
