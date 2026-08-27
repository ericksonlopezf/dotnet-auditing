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
    /// Executes ResultResponseTypes_GeneratesResultFactory.
    /// </summary>
    [Fact]
    public void ResultResponseTypes_GeneratesResultFactory()
    {
        string source = @"
using EricksonLopez.Result;

namespace EricksonLopez.Result
{
    /// <summary>
    /// Represents Error.
    /// </summary>
    public class Error { }
    /// <summary>
    /// Represents Result.
    /// </summary>
    public class Result
    {
        public static Result Failure(Error error) => new Result();
    }
}

namespace TestApp
{
    /// <summary>
    /// Represents MyCommand.
    /// </summary>
    public class MyCommand : ICommand<Result> { }
    
    /// <summary>
    /// Represents MyCommandHandler.
    /// </summary>
    public class MyCommandHandler : ICommandHandler<MyCommand, Result>
    {
        /// <summary>
        /// Executes Handle.
        /// </summary>
        public ValueTask<Result> Handle(MyCommand command, CancellationToken ct) => new(new Result());
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().BeEmpty();

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var diCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.Contains("services.AddSingleton<global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result>, ResultFactory0>();", diCode);
        Assert.Contains("internal sealed class ResultFactory0 : global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result>", diCode);
    }
}

