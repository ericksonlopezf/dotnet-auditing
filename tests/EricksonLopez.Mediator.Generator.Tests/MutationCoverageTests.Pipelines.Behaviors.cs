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
    public void OpenGenericBehaviorWith3Params_ProducesELM007()
    {
        string source = @"

namespace TestApp
{
    [UseBehavior(typeof(ThreeParamBehavior<,,>))]
    public class ThreeParamCmd : ICommand<int> { }

    public class ThreeParamCmdHandler : ICommandHandler<ThreeParamCmd, int>
    {
        public ValueTask<int> Handle(ThreeParamCmd cmd, CancellationToken ct) => new(1);
    }

    public class ThreeParamBehavior<T1, T2, T3> : IPipelineBehavior<ThreeParamCmd, int>
    {
        public ValueTask<int> Handle<TNext>(ThreeParamCmd req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "ELM007" && d.GetMessage().Contains("has 3 type parameters"));
    }

    [Fact]
    public void OpenGenericNotificationBehaviorWith2Params_ProducesELM007()
    {
        string source = @"

namespace TestApp
{
    [UseBehavior(typeof(TwoParamNotifBehavior<,>))]
    public class TwoParamNotif : INotification { }

    public class TwoParamNotifHandler : INotificationHandler<TwoParamNotif>
    {
        public ValueTask Handle(TwoParamNotif n, CancellationToken ct) => default;
    }

    public class TwoParamNotifBehavior<T1, T2> : INotificationBehavior<TwoParamNotif>
    {
        public ValueTask Handle<TNext>(TwoParamNotif n, TNext next, CancellationToken ct) where TNext : struct, INotificationNext => next.InvokeAsync();
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Contains(diagnostics, d => d.Id == "ELM007" && d.GetMessage().Contains("Only 1 is supported (TNotification)"));
    }

    [Fact]
    public void NotificationBehavior_ExactTypeAndBaseClassConstraints_MatchCorrectly()
    {
        string source = @"

namespace TestApp
{
    public class BaseNotif : INotification { }
    public class DerivedNotif : BaseNotif { }

    public class DerivedNotifHandler : INotificationHandler<DerivedNotif>
    {
        public ValueTask Handle(DerivedNotif n, CancellationToken ct) => default;
    }

    [UseBehavior(typeof(BaseNotifBehavior<>))]
    [UseBehavior(typeof(ExactNotifBehavior<>))]
    public class TargetNotif : BaseNotif { }

    public class TargetNotifHandler : INotificationHandler<TargetNotif>
    {
        public ValueTask Handle(TargetNotif n, CancellationToken ct) => default;
    }

    public class BaseNotifBehavior<T> : INotificationBehavior<T> where T : BaseNotif
    {
        public ValueTask Handle<TNext>(T n, TNext next, CancellationToken ct) where TNext : struct, INotificationNext => next.InvokeAsync();
    }

    public class ExactNotifBehavior<T> : INotificationBehavior<T> where T : TargetNotif
    {
        public ValueTask Handle<TNext>(T n, TNext next, CancellationToken ct) where TNext : struct, INotificationNext => next.InvokeAsync();
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("TargetNotifBehavior0Next", dispatcherCode);
        Assert.Contains("TargetNotifBehavior1Next", dispatcherCode);
    }

    [Fact]
    public void NotificationBehavior_ConcreteClassNotImplementingInterface_IsIgnored()
    {
        string source = @"

namespace TestApp
{
    public class UnrelatedClass { }

    [UseBehavior(typeof(UnrelatedClass))]
    public class PlainNotif : INotification { }

    public class PlainNotifHandler : INotificationHandler<PlainNotif>
    {
        public ValueTask Handle(PlainNotif n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();
        var diCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.DoesNotContain("PlainNotifBehavior0Next", dispatcherCode);
        Assert.DoesNotContain("UnrelatedClass", dispatcherCode);
        Assert.DoesNotContain("UnrelatedClass", diCode);
    }

    [Fact]
    public void Notification_SequentialAggregateExceptions_WithBehavior_GeneratesTryCatchInNextStruct()
    {
        string source = @"

namespace TestApp
{
    [PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
    [UseBehavior(typeof(NotifLoggingBehavior))]
    public class AggEvent : INotification { }

    public class AggEventHandler1 : INotificationHandler<AggEvent>
    {
        public ValueTask Handle(AggEvent n, CancellationToken ct) => default;
    }

    public class AggEventHandler2 : INotificationHandler<AggEvent>
    {
        public ValueTask Handle(AggEvent n, CancellationToken ct) => default;
    }

    public class NotifLoggingBehavior : INotificationBehavior<AggEvent>
    {
        public ValueTask Handle<TNext>(AggEvent n, TNext next, CancellationToken ct)
            where TNext : struct, INotificationNext => next.InvokeAsync();
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("internal readonly struct AggEventNotificationNext : INext", dispatcherCode);
        Assert.Contains("List<Exception> exceptions = null;", dispatcherCode);
        Assert.Contains("await _sp.GetRequiredService<global::TestApp.AggEventHandler1>().Handle(_n, _ct).ConfigureAwait(false);", dispatcherCode);
        Assert.Contains("await _sp.GetRequiredService<global::TestApp.AggEventHandler2>().Handle(_n, _ct).ConfigureAwait(false);", dispatcherCode);
        Assert.Contains("} catch (Exception ex) {", dispatcherCode);
        Assert.Contains("exceptions.Add(ex);", dispatcherCode);
        Assert.Contains("if (exceptions is not null) throw new global::EricksonLopez.Mediator.NotificationHandlerAggregateException(exceptions);", dispatcherCode);
    }

    [Fact]
    public void SingleArg_UseBehavior_And_UseGlobalBehavior_AppliedCorrectly()
    {
        string source = @"

[assembly: UseGlobalBehavior(typeof(TestApp.AssemblyGlobalBehavior))]

namespace TestApp
{
    public class AssemblyGlobalBehavior : IPipelineBehavior<SingleArgCmd, int>
    {
        public ValueTask<int> Handle<TNext>(SingleArgCmd req, TNext next, CancellationToken ct)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }

    public class SpecificSingleArgBehavior : IPipelineBehavior<SingleArgCmd, int>
    {
        public ValueTask<int> Handle<TNext>(SingleArgCmd req, TNext next, CancellationToken ct)
            where TNext : struct, INext<int> => next.InvokeAsync();
    }

    [UseBehavior(typeof(SpecificSingleArgBehavior))]
    public class SingleArgCmd : ICommand<int> { }

    public class SingleArgCmdHandler : ICommandHandler<SingleArgCmd, int>
    {
        public ValueTask<int> Handle(SingleArgCmd cmd, CancellationToken ct) => new(42);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("AssemblyGlobalBehavior", dispatcherCode);
        Assert.Contains("SpecificSingleArgBehavior", dispatcherCode);
    }

    [Fact]
    public void GlobalBehavior_WithTwoTypeArguments_OnCompilationWithNotification_DoesNotReportELM007()
    {
        string source = @"

[assembly: UseGlobalBehavior(typeof(TestApp.GlobalPipelineBehavior<,>))]

namespace TestApp
{
    public class GlobalPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public ValueTask<TResponse> Handle<TNext>(TRequest req, TNext next, CancellationToken ct)
            where TNext : struct, INext<TResponse> => next.InvokeAsync();
    }

    public class SimpleNotif : INotification { }
    public class SimpleNotifHandler : INotificationHandler<SimpleNotif>
    {
        public ValueTask Handle(SimpleNotif n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void UseGlobalBehavior_WithDefaultOrder_AndCustomOrder()
    {
        string source = @"

[assembly: UseGlobalBehavior(typeof(TestApp.GlobalBehavior1<,>))]
[assembly: UseGlobalBehavior(typeof(TestApp.GlobalBehavior2<,>), 5)]

namespace TestApp
{
    public class GlobalBehavior1<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    {
        public ValueTask<TRes> Handle<TNext>(TReq req, TNext next, CancellationToken ct) where TNext : struct, INext<TRes> => next.InvokeAsync();
    }

    public class GlobalBehavior2<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    {
        public ValueTask<TRes> Handle<TNext>(TReq req, TNext next, CancellationToken ct) where TNext : struct, INext<TRes> => next.InvokeAsync();
    }

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
        Assert.Empty(diagnostics);

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("var b0 = _serviceProvider.GetRequiredService<global::TestApp.GlobalBehavior1<global::TestApp.SimpleCmd, int>>();", dispatcherCode);
        Assert.Contains("var b1 = _serviceProvider.GetRequiredService<global::TestApp.GlobalBehavior2<global::TestApp.SimpleCmd, int>>();", dispatcherCode);
    }

    [Fact]
    public void NotificationBehavior_OpenGeneric_WithConstraints_And_UnsupportedMultiParam()
    {
        string source = @"

namespace TestApp
{
    public interface ICustomNotification : INotification { }
    public class EventA : ICustomNotification { }

    public class ConstrainedBehavior<TNotif> : INotificationBehavior<TNotif>
        where TNotif : ICustomNotification
    {
        public ValueTask Handle<TNext>(TNotif n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
    }

    [UseBehavior(typeof(ConstrainedBehavior<>))]
    public class EventB : INotification { }

    public class EventBHandler : INotificationHandler<EventB>
    {
        public ValueTask Handle(EventB n, CancellationToken ct) => default;
    }

    [UseBehavior(typeof(MultiParamBehavior<,>))]
    public class EventC : INotification { }

    public class MultiParamBehavior<T1, T2> : INotificationBehavior<EventC>
    {
        public ValueTask Handle<TNext>(EventC n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
    }

    public class EventCHandler : INotificationHandler<EventC>
    {
        public ValueTask Handle(EventC n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM007");

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.DoesNotContain("ConstrainedBehavior<global::TestApp.EventB>", dispatcherCode);
    }

    [Fact]
    public void UseBehaviorAttribute_SingleArgument_And_TwoArguments()
    {
        string source = @"

namespace TestApp
{
    public class Behave1<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    {
        public ValueTask<TRes> Handle<TNext>(TReq req, TNext next, CancellationToken ct) where TNext : struct, INext<TRes> => next.InvokeAsync();
    }
    public class Behave2<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    {
        public ValueTask<TRes> Handle<TNext>(TReq req, TNext next, CancellationToken ct) where TNext : struct, INext<TRes> => next.InvokeAsync();
    }

    [UseBehavior(typeof(Behave1<,>))]
    [UseBehavior(typeof(Behave2<,>), 10)]
    public class OrderCmd : ICommand<int> { }

    public class OrderCmdHandler : ICommandHandler<OrderCmd, int>
    {
        public ValueTask<int> Handle(OrderCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics);

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("var b0 = _serviceProvider.GetRequiredService<global::TestApp.Behave1<global::TestApp.OrderCmd, int>>();", dispatcherCode);
        Assert.Contains("var b1 = _serviceProvider.GetRequiredService<global::TestApp.Behave2<global::TestApp.OrderCmd, int>>();", dispatcherCode);
    }

    [Fact]
    public void ConcreteBehavior_WithMultipleInterfaces_IsRegisteredAndInvoked()
    {
        string source = @"

namespace TestApp
{
    public class SimpleCmd : ICommand<int> { }
    public class SimpleCmdHandler : ICommandHandler<SimpleCmd, int>
    {
        public ValueTask<int> Handle(SimpleCmd cmd, CancellationToken ct) => new(1);
    }

    public class MultiInterfaceBehavior : IDisposable, IPipelineBehavior<SimpleCmd, int>
    {
        public ValueTask<int> Handle<TNext>(SimpleCmd req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
        public void Dispose() { }
    }

    [UseBehavior(typeof(MultiInterfaceBehavior))]
    public class CmdWithConcreteBehavior : ICommand<int> { }

    public class CmdWithConcreteBehaviorHandler : ICommandHandler<CmdWithConcreteBehavior, int>
    {
        public ValueTask<int> Handle(CmdWithConcreteBehavior cmd, CancellationToken ct) => new(1);
    }

    public class SimpleNotif : INotification { }
    public class SimpleNotifHandler : INotificationHandler<SimpleNotif>
    {
        public ValueTask Handle(SimpleNotif n, CancellationToken ct) => default;
    }

    public class MultiInterfaceNotifBehavior : IDisposable, INotificationBehavior<SimpleNotif>
    {
        public ValueTask Handle<TNext>(SimpleNotif n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
        public void Dispose() { }
    }

    [UseBehavior(typeof(MultiInterfaceNotifBehavior))]
    public class NotifWithConcreteBehavior : INotification { }

    public class NotifWithConcreteBehaviorHandler : INotificationHandler<NotifWithConcreteBehavior>
    {
        public ValueTask Handle(NotifWithConcreteBehavior n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("MultiInterfaceBehavior", dispatcherCode);
        Assert.Contains("MultiInterfaceNotifBehavior", dispatcherCode);
    }

    [Fact]
    public void NotificationPipeline_DefaultSequentialStrategy_InvokesHandlers()
    {
        string source = @"

namespace TestApp
{
    public class SequentialNotif : INotification { }

    public class SeqBehavior<TNotif> : INotificationBehavior<TNotif> where TNotif : INotification
    {
        public ValueTask Handle<TNext>(TNotif n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
    }

    [UseBehavior(typeof(SeqBehavior<>))]
    public class EventWithBehaviors : INotification { }

    public class EventWithBehaviorsHandler : INotificationHandler<EventWithBehaviors>
    {
        public ValueTask Handle(EventWithBehaviors n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("await _sp.GetRequiredService<global::TestApp.EventWithBehaviorsHandler>().Handle(_n, _ct).ConfigureAwait(false);", dispatcherCode);
    }

    [Fact]
    public void Constrained1ParamBehavior_UnsatisfiedConstraint_IsNotApplied()
    {
        string source = @"

namespace TestApp
{
    public interface IMarker { }

    public class Constrained1Behavior<TReq> : IPipelineBehavior<TReq, int> where TReq : IMarker
    {
        public ValueTask<int> Handle<TNext>(TReq req, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }

    [UseBehavior(typeof(Constrained1Behavior<>))]
    public class TestCmd : ICommand<int> { }

    public class TestCmdHandler : ICommandHandler<TestCmd, int>
    {
        public ValueTask<int> Handle(TestCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.DoesNotContain("Constrained1Behavior", dispatcherCode);
    }

    [Fact]
    public void Notification_WithNonNotificationBehavior_IsNotApplied()
    {
        string source = @"

namespace TestApp
{
    public class NonBehaviorClass { }

    [UseBehavior(typeof(NonBehaviorClass))]
    public class CustomNotif : INotification { }

    public class CustomNotifHandler : INotificationHandler<CustomNotif>
    {
        public ValueTask Handle(CustomNotif n, CancellationToken ct) => default;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.DoesNotContain("NonBehaviorClass", dispatcherCode);
    }

    [Fact]
    public void PipelineAndNotificationBehaviors_WithoutStreamRequests_ProduceNoDiagnostics()
    {
        string source = @"

namespace TestApp
{
    public class SimpleReq : ICommand<int> { }
    public class SimpleReqHandler : ICommandHandler<SimpleReq, int>
    {
        public ValueTask<int> Handle(SimpleReq req, CancellationToken ct) => new(1);
    }

    public class PipeBehav<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : ICommand<TRes>
    {
        public ValueTask<TRes> Handle<TNext>(TReq req, TNext next, CancellationToken ct) where TNext : struct, INext<TRes> => next.InvokeAsync();
    }

    public class NotifBehav<TNotif> : INotificationBehavior<TNotif> where TNotif : INotification
    {
        public ValueTask Handle<TNext>(TNotif n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.Empty(diagnostics);
    }
}
