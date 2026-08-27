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
    public void InvalidStreamHandlerSignature_ReportsELM011_AndDoesNotEmitInDispatcher()
    {
        string source = @"

namespace TestApp
{
    public class BadStreamReq : IStreamRequest<string> { }

    public class BadStreamHandler : IStreamRequestHandler<BadStreamReq, string>
    {
        // Missing CancellationToken parameter
        public async IAsyncEnumerable<string> Handle(BadStreamReq req)
        {
            yield return ""test"";
        }
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "ELM011");

        var dispatcherCode = outComp.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("GeneratedMediator.g.cs"))?.ToString();

        Assert.True(dispatcherCode == null || !dispatcherCode.Contains("BadStreamHandler"));
    }

    [Fact]
    public void StreamRequest_And_Handler_ValidAndInvalidSignature_ReportsDiagnosticsOrRegisters()
    {
        string source = @"

namespace TestApp
{
    public class StreamA : IStreamRequest<string> { }
    public class StreamAHandler : IStreamRequestHandler<StreamA, string>
    {
        public async IAsyncEnumerable<string> Handle(StreamA req, CancellationToken ct)
        {
            await Task.Yield();
            yield return ""ok"";
        }
    }

    public class StreamB : IStreamRequest<string> { }
    public class StreamBBadHandler : IStreamRequestHandler<StreamB, string>
    {
        public async IAsyncEnumerable<int> Handle(StreamB req, CancellationToken ct)
        {
            await Task.Yield();
            yield return 1;
        }
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM011");

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("case global::TestApp.StreamA req:");
    }

}

