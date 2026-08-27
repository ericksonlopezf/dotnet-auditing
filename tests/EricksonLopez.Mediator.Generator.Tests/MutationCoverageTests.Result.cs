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
    public void ResultFactories_CompileAndExecuteFailureCreation()
    {
        string source = @"
using EricksonLopez.Mediator;
using EricksonLopez.Result;

namespace TestApp
{
    public class NestedResultCommand : ICommand<Result<Result<int>>> { }
    public class NestedResultHandler : ICommandHandler<NestedResultCommand, Result<Result<int>>>
    {
        public ValueTask<Result<Result<int>>> Handle(NestedResultCommand command, CancellationToken ct) => new(Result<Result<int>>.Success(Result<int>.Success(100)));
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().Contain("internal sealed class ResultFactory0 : global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<global::EricksonLopez.Result.Result<int>>>");
        diCode.Should().Contain("public global::EricksonLopez.Result.Result<global::EricksonLopez.Result.Result<int>> CreateFailure(global::EricksonLopez.Result.Error error)");
        diCode.Should().Contain("return global::EricksonLopez.Result.Result<global::EricksonLopez.Result.Result<int>>.Failure(error);");
    }

    [Fact]
    public void ResultFactory_GeneratedExactCode_HasFailureMethodAndBraces()
    {
        string source = @"

namespace TestApp
{
    public class ResultCmd : ICommand<Result<string>> { }
    public class ResultCmdHandler : ICommandHandler<ResultCmd, Result<string>>
    {
        public ValueTask<Result<string>> Handle(ResultCmd command, CancellationToken ct) => new(Result<string>.Success(""ok""));
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().Contain("internal sealed class ResultFactory0 : global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<string>>");
        diCode.Should().Contain("public global::EricksonLopez.Result.Result<string> CreateFailure(global::EricksonLopez.Result.Error error)");
        diCode.Should().Contain("return global::EricksonLopez.Result.Result<string>.Failure(error);");
    }

    [Fact]
    public void QueryWithResultPattern_RegistersResultFactory()
    {
        string source = @"

namespace TestApp
{
    public class ResultQuery : IQuery<Result<int>> { }
    public class ResultQueryHandler : IQueryHandler<ResultQuery, Result<int>>
    {
        public ValueTask<Result<int>> Handle(ResultQuery query, CancellationToken ct) => new(Result<int>.Success(100));
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.Contains("services.AddSingleton<global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<int>>, ResultFactory0>();", diCode);
    }

    [Fact]
    public void DependencyInjectionGenerator_MultipleResultTypes_GeneratesAllFactoriesWithExactIndicesAndBraces()
    {
        string source = @"

namespace TestApp
{
    public class CmdA : ICommand<Result<int>> { }
    public class CmdAHandler : ICommandHandler<CmdA, Result<int>>
    {
        public ValueTask<Result<int>> Handle(CmdA cmd, CancellationToken ct) => new(Result<int>.Success(1));
    }

    public class CmdB : ICommand<Result<string>> { }
    public class CmdBHandler : ICommandHandler<CmdB, Result<string>>
    {
        public ValueTask<Result<string>> Handle(CmdB cmd, CancellationToken ct) => new(Result<string>.Success(""ok""));
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        var normalizedDiCode = diCode.Replace("\r\n", "\n");
        var expectedFactory0 = @"    internal sealed class ResultFactory0 : global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<int>>
    {
        public global::EricksonLopez.Result.Result<int> CreateFailure(global::EricksonLopez.Result.Error error)
        {
            return global::EricksonLopez.Result.Result<int>.Failure(error);
        }
    }".Replace("\r\n", "\n");

        var expectedFactory1 = @"    internal sealed class ResultFactory1 : global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<string>>
    {
        public global::EricksonLopez.Result.Result<string> CreateFailure(global::EricksonLopez.Result.Error error)
        {
            return global::EricksonLopez.Result.Result<string>.Failure(error);
        }
    }".Replace("\r\n", "\n");

        normalizedDiCode.Should().Contain(expectedFactory0);
        normalizedDiCode.Should().Contain(expectedFactory1);
        Assert.Contains("services.AddSingleton<global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<int>>, ResultFactory0>();", diCode);
        Assert.Contains("services.AddSingleton<global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result<string>>, ResultFactory1>();", diCode);
    }

    [Fact]
    public void Result_Type_From_Other_Namespace_Not_Treated_As_Result_Pattern()
    {
        string source = @"

namespace OtherNamespace
{
    public class Result { }
}

namespace TestApp
{
    public class OtherResultCmd : ICommand<OtherNamespace.Result> { }
    public class OtherResultCmdHandler : ICommandHandler<OtherResultCmd, OtherNamespace.Result>
    {
        public ValueTask<OtherNamespace.Result> Handle(OtherResultCmd cmd, CancellationToken ct) => new(new OtherNamespace.Result());
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().NotContain("IResultFactory<OtherNamespace.Result>");
    }

    [Fact]
    public void Command_ReturningResultError_DoesNotGenerateResultFactory()
    {
        string source = @"

namespace TestApp
{
    public class ErrorCmd : ICommand<Error> { }
    public class ErrorCmdHandler : ICommandHandler<ErrorCmd, Error>
    {
        public ValueTask<Error> Handle(ErrorCmd cmd, CancellationToken ct) => new(Error.NotFound(""msg""));
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().NotContain("IResultFactory<global::EricksonLopez.Result.Error>");
    }

}

