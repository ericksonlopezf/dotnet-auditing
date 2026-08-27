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
    public void InvalidNotificationHandlerSignature_ReportsELM004_AndDoesNotEmitInDispatcher()
    {
        string source = @"

namespace TestApp
{
    public class InvalidNotif : INotification { }

    public class BadNotifHandler : INotificationHandler<InvalidNotif>
    {
        // Bad signature: returns void instead of ValueTask
        public void Handle(InvalidNotif n, CancellationToken ct) { }
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "ELM004");

        var dispatcherCode = outComp.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("GeneratedMediator.g.cs"))?.ToString();

        Assert.True(dispatcherCode == null || !dispatcherCode.Contains("BadNotifHandler"));
    }

    [Fact]
    public void DispatcherGenerator_SequentialAggregateExceptions_GeneratesListHandling()
    {
        string source = @"

namespace TestApp
{
    [PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
    public class AggregateEvent : INotification { }

    public class AggregateHandler1 : INotificationHandler<AggregateEvent>
    {
        public ValueTask Handle(AggregateEvent n, CancellationToken ct) => default;
    }
    public class AggregateHandler2 : INotificationHandler<AggregateEvent>
    {
        public ValueTask Handle(AggregateEvent n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        var normalizedDispCode = dispatcherCode.Replace("\r\n", "\n");
        var expectedCatch = @"            } catch (Exception ex) {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }".Replace("\r\n", "\n");

        normalizedDispCode.Should().Contain(expectedCatch);
        dispatcherCode.Should().Contain("if (exceptions is not null) throw new global::EricksonLopez.Mediator.NotificationHandlerAggregateException(exceptions);");
    }

    [Fact]
    public void PublishStrategy_Default_And_Invalid_FallsBackToSequential()
    {
        string source = @"

namespace TestApp
{
    [PublishStrategy((PublishStrategy)99)]
    public class CustomEvent : INotification { }

    public class CustomEventHandler : INotificationHandler<CustomEvent>
    {
        public ValueTask Handle(CustomEvent n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().NotContain("var tasks = new Task[");
        dispatcherCode.Should().NotContain("NotificationHandlerAggregateException");
    }

}

