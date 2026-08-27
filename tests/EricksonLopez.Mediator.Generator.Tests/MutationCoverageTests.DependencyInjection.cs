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
    public void CustomServiceLifetime_OnNotificationAndStreamHandlers_RegistersCorrectLifetime()
    {
        string source = @"

namespace TestApp
{
    public class CustomEvent : INotification { }

    [ServiceLifetime(HandlerLifetime.Singleton)]
    public class SingletonCustomEventHandler : INotificationHandler<CustomEvent>
    {
        public ValueTask Handle(CustomEvent n, CancellationToken ct) => default;
    }

    public class CustomStream : IStreamRequest<int> { }

    [ServiceLifetime(HandlerLifetime.Scoped)]
    public class ScopedCustomStreamHandler : IStreamRequestHandler<CustomStream, int>
    {
        public async IAsyncEnumerable<int> Handle(CustomStream req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
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
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diTree = outComp.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs"));
        diTree.Should().NotBeNull();
        var diCode = diTree.ToString();

        diCode.Should().Contain("services.AddSingleton<global::TestApp.SingletonCustomEventHandler>();");
        diCode.Should().Contain("services.AddScoped<global::TestApp.ScopedCustomStreamHandler>();");
    }

    [Fact]
    public void DeepNestedHandler_DiscoveredCorrectly()
    {
        string source = @"

namespace TestApp
{
    public class Level1
    {
        public class Level2
        {
            public class NestedCmd : ICommand<int> { }
            public class NestedCmdHandler : ICommandHandler<NestedCmd, int>
            {
                public ValueTask<int> Handle(NestedCmd cmd, CancellationToken ct) => new(99);
            }
        }
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().Contain("services.AddTransient<global::TestApp.Level1.Level2.NestedCmdHandler>();");
    }

    [Fact]
    public void ForeignNamespace_ICommand_NotRegisteredAsMediatorRequest()
    {
        string source = @"

namespace ForeignNamespace
{
    public interface ICommand<TResponse> { }
}

namespace TestApp
{
    public class ForeignCmd : ForeignNamespace.ICommand<int> { }

    public class RealCmd : ICommand<int> { }
    public class RealCmdHandler : ICommandHandler<RealCmd, int>
    {
        public ValueTask<int> Handle(RealCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("RealCmd");
        dispatcherCode.Should().NotContain("ForeignCmd");
    }

    [Fact]
    public void ExternalAssemblyDiscovery_WithMultipleInterfaces_And_NestedTypes()
    {
        string extSource = @"

namespace ExternalLib
{
    public class ExtMarker { }

    public class ExtCommand : ICommand<int> { }

    public class ExtHandlerWithMultipleInterfaces : ICommandHandler<ExtCommand, int>, IDisposable
    {
        public ValueTask<int> Handle(ExtCommand cmd, CancellationToken ct) => new(77);
        public void Dispose() { }
    }

    public class ExtOuter
    {
        public class ExtNestedHandler : ICommandHandler<ExtCommand, int>, IDisposable
        {
            public ValueTask<int> Handle(ExtCommand cmd, CancellationToken ct) => new(88);
            public void Dispose() { }
        }

        public class ExtMiddle
        {
            public class ExtDeepNestedHandler : ICommandHandler<ExtCommand, int>, IDisposable
            {
                public ValueTask<int> Handle(ExtCommand cmd, CancellationToken ct) => new(99);
                public void Dispose() { }
            }
        }
    }
}";
        var extComp = CreateCompilation(extSource, "ExternalLib");
        var extRef = extComp.ToMetadataReference();

        string appSource = @"

[assembly: DiscoverHandlers(typeof(ExternalLib.ExtMarker))]

namespace MainApp
{
    public class Program { }
}";
        var appComp = CreateCompilation(appSource, "MainApp")
            .AddReferences(extRef);

        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(appComp, out var outComp, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "ELM002"); // Multiple handlers for ExtCommand

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().Contain("ExtHandlerWithMultipleInterfaces");
        diCode.Should().Contain("ExtNestedHandler");
        diCode.Should().Contain("ExtDeepNestedHandler");
    }

    [Fact]
    public void DispatcherGenerator_StaticMethods_GeneratedInMediator()
    {
        string source = @"

namespace TestApp
{
    public class SimpleCmd : ICommand<int> { }
    public class SimpleCmdHandler : ICommandHandler<SimpleCmd, int>
    {
        public ValueTask<int> Handle(SimpleCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("public static ValueTask<TResponse> SendCommandStatic<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)", dispatcherCode);
        Assert.Contains("=> StaticMediator.SendCommand<TCommand, TResponse>(command, cancellationToken);", dispatcherCode);
        Assert.Contains("public static ValueTask<TResponse> SendQueryStatic<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)", dispatcherCode);
        Assert.Contains("=> StaticMediator.SendQuery<TQuery, TResponse>(query, cancellationToken);", dispatcherCode);
        Assert.Contains("public static ValueTask PublishStatic<TNotification>(TNotification notification, CancellationToken cancellationToken = default)", dispatcherCode);
        Assert.Contains("=> StaticMediator.Publish(notification, cancellationToken);", dispatcherCode);
    }

    [Fact]
    public void ServiceLifetimeAttribute_Scoped_And_Default()
    {
        string source = @"

namespace TestApp
{
    [ServiceLifetime(HandlerLifetime.Scoped)]
    public class ScopedCmd : ICommand<int> { }
    
    [ServiceLifetime(HandlerLifetime.Scoped)]
    public class ScopedCmdHandler : ICommandHandler<ScopedCmd, int>
    {
        public ValueTask<int> Handle(ScopedCmd cmd, CancellationToken ct) => new(1);
    }

    public class TransCmd : ICommand<int> { }

    [ServiceLifetime((HandlerLifetime)99)]
    public class TransCmdHandler : ICommandHandler<TransCmd, int>
    {
        public ValueTask<int> Handle(TransCmd cmd, CancellationToken ct) => new(2);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().Contain("services.AddScoped<global::TestApp.ScopedCmdHandler>();");
        diCode.Should().Contain("services.AddTransient<global::TestApp.TransCmdHandler>();");
    }

    [Fact]
    public void DiscoverHandlers_DuplicateAssemblyReference_CollectsOnlyOnce()
    {
        string source = @"

[assembly: DiscoverHandlers(typeof(TestApp.MarkerA))]
[assembly: DiscoverHandlers(typeof(TestApp.MarkerA))]

namespace TestApp
{
    public class MarkerA { }
    public class SimpleCmd : ICommand<int> { }
    public class SimpleCmdHandler : ICommandHandler<SimpleCmd, int>
    {
        public ValueTask<int> Handle(SimpleCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        int occurrences = diCode.Split(new[] { "services.AddTransient<global::TestApp.SimpleCmdHandler>();" }, StringSplitOptions.None).Length - 1;
        occurrences.Should().Be(1);
    }

    [Fact]
    public void NonMediatorInterface_NamedICommand_IsNotRegistered()
    {
        string source = @"

namespace CustomNamespace
{
    public interface ICommand<T> { }
    public interface ICommandHandler<TReq, TRes> { }
    public class CustomCmd : ICommand<int> { }
    public class FakeHandler : ICommandHandler<ValidApp.ValidCmd, int> { }
}

namespace ValidApp
{
    public class ValidCmd : ICommand<int> { }
    public class ValidCmdHandler : ICommandHandler<ValidCmd, int>
    {
        public ValueTask<int> Handle(ValidCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().NotContain("CustomCmd");
        dispatcherCode.Should().NotContain("FakeHandler");
        dispatcherCode.Should().Contain("ValidCmd");
    }

    [Fact]
    public void HandlerAndNotification_WithNonMediatorAttributes_AreProcessedCorrectly()
    {
        string source = @"
using System.ComponentModel;

namespace TestApp
{
    [Description(""Some notification description"")]
    public class DescribedEvent : INotification { }

    [Description(""Some handler description"")]
    public class DescribedEventHandler : INotificationHandler<DescribedEvent>
    {
        public ValueTask Handle(DescribedEvent n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        diCode.Should().Contain("services.AddTransient<global::TestApp.DescribedEventHandler>();");
    }
}

