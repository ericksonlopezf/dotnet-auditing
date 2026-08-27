// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class EdgeCaseCoverageTests
{
    private static Compilation CreateCompilation(string source)
        => RoslynTestHelper.CreateCompilation(source, "CompEdgeCases");

    [Fact]
    public void ServiceLifetimeAttribute_InvalidValue_DefaultsToTransient()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    [ServiceLifetime((HandlerLifetime)999)]
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var diCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.Contains("services.AddTransient<global::TestApp.MyCommandHandler>();", diCode);
    }

    [Fact]
    public void OpenGenericBehavior_WithTwoTypeArguments_ClosesSuccessfully()
    {
        string source = @"

namespace TestApp
{
    public class MyCommand : ICommand<int> { }
    
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
    }

    public class OpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<TResponse> => next.InvokeAsync();
    }

    [UseBehavior(typeof(OpenBehavior<,>))]
    public class MyCommand2 : ICommand<int> { }
    public class MyCommandHandler2 : ICommandHandler<MyCommand2, int>
    {
        public ValueTask<int> Handle(MyCommand2 command, CancellationToken ct) => new(42);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("global::TestApp.OpenBehavior<global::TestApp.MyCommand2, int>", dispatcherCode);
    }

    [Fact]
    public void NotificationBehavior_GlobalAndOpenGeneric_Validation()
    {
        string source = @"

// Global pipeline behavior (TypeParams=2). Will be ignored for notifications gracefully.
[assembly: UseGlobalBehavior(typeof(TestApp.GlobalPipeBehavior<,>))]
// Global notification behavior (TypeParams=1). Will be applied to notifications.
[assembly: UseGlobalBehavior(typeof(TestApp.GlobalNotifBehavior<>))]

namespace TestApp
{
    public class GlobalPipeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken ct) where TNext : struct, INext<TResponse> => next.InvokeAsync();
    }

    public class GlobalNotifBehavior<TNotification> : INotificationBehavior<TNotification>
    {
        public ValueTask Handle<TNext>(TNotification notification, TNext next, CancellationToken ct) where TNext : struct, INext<Unit> => next.InvokeAsync();
    }

    public class SpecificNotifBehavior<TNotification> : INotificationBehavior<TNotification>
    {
        public ValueTask Handle<TNext>(TNotification notification, TNext next, CancellationToken ct) where TNext : struct, INext<Unit> => next.InvokeAsync();
    }

    public class ConcretePipeBehavior : IPipelineBehavior<MyNotif, int>
    {
        public ValueTask<int> Handle<TNext>(MyNotif request, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }

    [UseBehavior(typeof(SpecificNotifBehavior<>))]
    [UseBehavior(typeof(GlobalPipeBehavior<,>))] // Invalid for notification, should produce ELM007
    [UseBehavior(typeof(ConcretePipeBehavior))] // Concrete, not a notification behavior, will be ignored
    public class MyNotif : INotification { }
    
    public class MyNotifHandler : INotificationHandler<MyNotif>
    {
        public ValueTask Handle(MyNotif notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "ELM007" && d.GetMessage().Contains("Only 1 is supported (TNotification) for notifications"));
    }

    [Fact]
    public void NestedTypes_And_ExternalTypes()
    {
        string source = @"

[assembly: DiscoverHandlers(typeof(TestApp.Nested.OuterClass))]

namespace TestApp.Nested
{
    public class OuterClass 
    {
        public class MyCommand : ICommand<int> { }
        
        [System.ComponentModel.Description(""Test"")] // Unrelated attribute to test foreach continue
        public class InnerHandler : ICommandHandler<MyCommand, int>
        {
            public ValueTask<int> Handle(MyCommand command, CancellationToken ct) => new(42);
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var diCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediatorExtensions.g.cs")).ToString();

        Assert.Contains("services.AddTransient<global::TestApp.Nested.OuterClass.InnerHandler>();", diCode);
    }

    [Fact]
    public void Dispatcher_GeneratesParallelNotification_WithBehaviors()
    {
        string source = @"

namespace TestApp
{
    public class MyNotifBehavior : INotificationBehavior<MyEvent>
    {
        public ValueTask Handle<TNext>(MyEvent notification, TNext next, CancellationToken ct) where TNext : struct, INext<Unit> => next.InvokeAsync();
    }

    [PublishStrategy(PublishStrategy.Parallel)]
    [UseBehavior(typeof(MyNotifBehavior))]
    public class MyEvent : INotification { }
    
    public class Handler1 : INotificationHandler<MyEvent>
    {
        public ValueTask Handle(MyEvent notification, CancellationToken ct) => default;
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("internal readonly struct MyEventNotificationNext", dispatcherCode);
        Assert.Contains("internal readonly struct MyEventBehavior0Next", dispatcherCode);
        Assert.Contains("var tasks = new Task[1];", dispatcherCode);
        Assert.Contains("tasks[0] = _sp.GetRequiredService<global::TestApp.Handler1>().Handle(_n, _ct).AsTask();", dispatcherCode);
        Assert.Contains("await Task.WhenAll(tasks).ConfigureAwait(false);", dispatcherCode);
    }

    [Fact]
    public void UnrelatedType_IsIgnoredByGenerator()
    {
        string source = @"

namespace TestApp
{
    public class UnrelatedClass : IDisposable
    {
        public void Dispose() { }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);
        Assert.DoesNotContain(outputCompilation.SyntaxTrees, t => t.FilePath.Contains("GeneratedMediator.g.cs"));
    }

    [Fact]
    public void BehaviorAttributes_GlobalBehaviorWithoutOrder_AssignedOrder0()
    {
        string source = @"
[assembly: TestApp.UseGlobalBehavior(typeof(TestApp.GlobalBehaviorNoOrder))]

namespace TestApp
{
    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
    public class UseGlobalBehaviorAttribute : System.Attribute
    {
        public UseGlobalBehaviorAttribute(System.Type behaviorType) { }
    }

    public class GlobalBehaviorNoOrder : IPipelineBehavior<MyCmd, int>
    {
        public ValueTask<int> Handle<TNext>(MyCmd request, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }

    public class MyCmd : ICommand<int> { }
    public class MyCmdHandler : ICommandHandler<MyCmd, int>
    {
        public ValueTask<int> Handle(MyCmd command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);
        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("GlobalBehaviorNoOrder", dispatcherCode);
    }

    [Fact]
    public void BehaviorAttributes_SpecificBehaviorWithoutOrder_AssignedOrder0()
    {
        string source = @"
namespace TestApp
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class UseBehaviorAttribute : System.Attribute
    {
        public UseBehaviorAttribute(System.Type behaviorType) { }
    }

    public class SpecificNoOrder : IPipelineBehavior<MyCmd, int>
    {
        public ValueTask<int> Handle<TNext>(MyCmd request, TNext next, CancellationToken ct) where TNext : struct, INext<int> => next.InvokeAsync();
    }

    [UseBehavior(typeof(SpecificNoOrder))]
    public class MyCmd : ICommand<int> { }
    public class MyCmdHandler : ICommandHandler<MyCmd, int>
    {
        public ValueTask<int> Handle(MyCmd command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);
        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("SpecificNoOrder", dispatcherCode);
    }

    [Fact]
    public void ValidationAttributes_CustomAttributesWithMissingOrNonMatchingArguments_CoversFallbackBranches()
    {
        string source = @"
namespace TestApp
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class ValidateRangeAttribute : System.Attribute
    {
        public ValidateRangeAttribute() { }
        public ValidateRangeAttribute(string notDouble1, string notDouble2) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class ValidateLengthAttribute : System.Attribute
    {
        public ValidateLengthAttribute() { }
        public ValidateLengthAttribute(string notInt1, string notInt2) { }
    }

    [ValidateRequest]
    public class CustomValidationCmd : ICommand<int>
    {
        [ValidateRange]
        public double Value1 { get; set; }

        [ValidateRange(""abc"", ""def"")]
        public double Value2 { get; set; }

        [ValidateLength]
        public string Text1 { get; set; } = """";

        [ValidateLength(""abc"", ""def"")]
        public string Text2 { get; set; } = """";
    }

    public class CustomValidationCmdHandler : ICommandHandler<CustomValidationCmd, int>
    {
        public ValueTask<int> Handle(CustomValidationCmd command, CancellationToken ct) => new(1);
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);
        var generatedSyntaxTrees = outputCompilation.SyntaxTrees.ToList();
        var dispatcherCode = generatedSyntaxTrees.First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("Value1", dispatcherCode);
        Assert.Contains("Value2", dispatcherCode);
        Assert.Contains("Text1", dispatcherCode);
        Assert.Contains("Text2", dispatcherCode);
    }
}



