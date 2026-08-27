// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class GeneratorExhaustiveMutationTests
{
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ICommand<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.Unsafe).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IAsyncEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask<>).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"))
        };

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void EquatableArray_LengthMismatch_AlwaysReturnsFalse()
    {
        var arr1 = new EquatableArray<int>(new[] { 1, 2 });
        var arr2 = new EquatableArray<int>(new[] { 1, 2, 3 });

        arr1.Equals(arr2).Should().BeFalse();
        arr2.Equals(arr1).Should().BeFalse();
        (arr1 == arr2).Should().BeFalse();
        (arr1 != arr2).Should().BeTrue();
    }

    [Fact]
    public void DependencyInjection_StreamHandler_RegistersLifetime()
    {
        var source = @"
using System.Collections.Generic;
using System.Threading;
using EricksonLopez.Mediator;

public record NumberStreamReq(int Count) : IStreamRequest<int>;

[ServiceLifetime(HandlerLifetime.Scoped)]
public class NumberStreamHandler : IStreamRequestHandler<NumberStreamReq, int>
{
    public async IAsyncEnumerable<int> Handle(NumberStreamReq request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 1;
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var diSource = runResult.GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediatorExtensions.g.cs")).ToString();

        diSource.Should().Contain("services.AddScoped<global::NumberStreamHandler>();");
    }

    [Fact]
    public void Dispatcher_EmitsPragma_AndCancellationChecks_AndStreamHandling()
    {
        var source = @"
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record Cmd() : ICommand<int>;
public class CmdH : ICommandHandler<Cmd, int> { public ValueTask<int> Handle(Cmd c, CancellationToken ct) => new(1); }

public record Qry() : IQuery<string>;
public class QryH : IQueryHandler<Qry, string> { public ValueTask<string> Handle(Qry q, CancellationToken ct) => new(""ok""); }

public record Evt() : INotification;
public class EvtH : INotificationHandler<Evt> { public ValueTask Handle(Evt e, CancellationToken ct) => default; }

public record Strm() : IStreamRequest<int>;
public class StrmH : IStreamRequestHandler<Strm, int>
{
    public async IAsyncEnumerable<int> Handle(Strm s, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { yield return 42; }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var dispSource = runResult.GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("#pragma warning disable CS8600, CS8604, CS1522");
        dispSource.Should().Contain("cancellationToken.ThrowIfCancellationRequested();");
        dispSource.Should().Contain("var handler = _serviceProvider.GetRequiredService<global::StrmH>();");
        dispSource.Should().Contain("var result = handler.Handle(req, cancellationToken);");
        dispSource.Should().Contain("return Unsafe.As<IAsyncEnumerable<int>, IAsyncEnumerable<TResponse>>(ref result);");
    }

    [Fact]
    public void Validation_NotEmpty_OnNullableString_EmitsCorrectCheck()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record NullableStrCmd([property: ValidateNotEmpty] string? Name) : ICommand<int>;
public class NullableStrCmdH : ICommandHandler<NullableStrCmd, int> { public ValueTask<int> Handle(NullableStrCmd c, CancellationToken ct) => new(1); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("if (string.IsNullOrEmpty(req.Name)) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Name must not be empty.\");");
    }

    [Fact]
    public void Validation_NotEmpty_OnGuid_EmitsGuidEmptyCheck()
    {
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record GuidCmd([property: ValidateNotEmpty] Guid Id) : ICommand<int>;
public class GuidCmdH : ICommandHandler<GuidCmd, int> { public ValueTask<int> Handle(GuidCmd c, CancellationToken ct) => new(1); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("if (req.Id == global::Guid.Empty) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Id must not be empty.\");");
    }

    [Fact]
    public void Validation_NotEmpty_OnCollection_EmitsEnumerableAnyCheck()
    {
        var source = @"
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record ListCmd([property: ValidateNotEmpty] List<string> Items) : ICommand<int>;
public class ListCmdH : ICommandHandler<ListCmd, int> { public ValueTask<int> Handle(ListCmd c, CancellationToken ct) => new(1); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("if (req.Items == null || !global::Enumerable.Any(req.Items)) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Items must not be empty.\");");
    }

    [Fact]
    public void Validation_Regex_WithQuotesAndDefaultMessage_EscapesCorrectly()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record RegexCmd([property: ValidateRegex(""^[a-z]+test$"")] string Code) : ICommand<int>;
public class RegexCmdH : ICommandHandler<RegexCmd, int> { public ValueTask<int> Handle(RegexCmd c, CancellationToken ct) => new(1); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("must match pattern");
        dispSource.Should().Contain("Regex.IsMatch");
        dispSource.Should().Contain("TimeSpan.FromSeconds(2)");
    }

    [Fact]
    public void Dispatcher_MultipleBehaviors_EmitsNestedStructPipeline()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(B1))]
[UseBehavior(typeof(B2))]
[UseBehavior(typeof(B3))]
public record MultiBReq() : ICommand<int>;

public class MultiBH : ICommandHandler<MultiBReq, int> { public ValueTask<int> Handle(MultiBReq r, CancellationToken ct) => new(1); }

public class B1 : IPipelineBehavior<MultiBReq, int> { public ValueTask<int> Handle<TNext>(MultiBReq r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync(); }
public class B2 : IPipelineBehavior<MultiBReq, int> { public ValueTask<int> Handle<TNext>(MultiBReq r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync(); }
public class B3 : IPipelineBehavior<MultiBReq, int> { public ValueTask<int> Handle<TNext>(MultiBReq r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync(); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("var next2 = new MultiBReqBehavior2Next(b2, handlerNext, req, cancellationToken);");
        dispSource.Should().Contain("var next1 = new MultiBReqBehavior1Next(b1, next2, req, cancellationToken);");
        dispSource.Should().Contain("var result = b0.Handle(req, next1, cancellationToken);");
    }

    [Fact]
    public void CustomResultTypeOutsideNamespace_NotTreatedAsEricksonLopezResult()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace CustomApp
{
    public class Result { public bool Ok { get; set; } }
    public record CustomResCmd() : ICommand<Result>;
    public class CustomResCmdH : ICommandHandler<CustomResCmd, Result> { public ValueTask<Result> Handle(CustomResCmd c, CancellationToken ct) => new(new Result()); }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediatorExtensions.g.cs")).ToString();

        // Should NOT generate ResultFactory for custom Result
        diSource.Should().NotContain("IResultFactory<global::CustomApp.Result>");
    }

    [Fact]
    public void Diagnostics_ELM004_InvalidBehaviorHandleSignature_ReportsError()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(BadBehavior))]
public record BadBReq() : ICommand<int>;

public class BadBH : ICommandHandler<BadBReq, int> { public ValueTask<int> Handle(BadBReq r, CancellationToken ct) => new(1); }

public class BadBehavior : IPipelineBehavior<BadBReq, int>
{
    public int Handle(BadBReq req) => 1;
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM004" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("BadBehavior"));
    }

    [Fact]
    public void Diagnostics_ELM006_NotificationHandlerNotFound_ReportsWarning()
    {
        var source = @"
using EricksonLopez.Mediator;
[DiscoverHandlers]
public record UnhandledNotification() : INotification;";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM006" && d.Severity == DiagnosticSeverity.Warning && d.GetMessage().Contains("UnhandledNotification"));
    }

    [Fact]
    public void Diagnostics_ELM009_StreamHandlerNotFound_ReportsError()
    {
        var source = @"
using EricksonLopez.Mediator;
[DiscoverHandlers]
public record UnhandledStream() : IStreamRequest<int>;";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM009" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("UnhandledStream"));
    }

    [Fact]
    public void Diagnostics_ELM011_InvalidStreamHandlerSignature_ReportsError()
    {
        var source = @"
using System.Threading;
using EricksonLopez.Mediator;

public record BrokenStreamReq() : IStreamRequest<int>;
public class BrokenStreamHandler : IStreamRequestHandler<BrokenStreamReq, int>
{
    public int Handle(BrokenStreamReq req) => 42;
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM011" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("BrokenStreamHandler"));
    }

    [Fact]
    public void Diagnostics_ELM007_OpenGenericBehaviorWithMoreThan2TypeParameters_ReportsError()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(TripleBehavior<,,>))]
public record TripleReq() : ICommand<int>;
public class TripleH : ICommandHandler<TripleReq, int> { public ValueTask<int> Handle(TripleReq r, CancellationToken ct) => new(1); }

public class TripleBehavior<TReq, TRes, TExtra>
{
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM007" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("TripleBehavior"));
    }

    [Fact]
    public void Diagnostics_ELM003_MultipleQueryHandlers_ReportsError()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record DuplicateQuery() : IQuery<int>;
public class QueryH1 : IQueryHandler<DuplicateQuery, int> { public ValueTask<int> Handle(DuplicateQuery q, CancellationToken ct) => new(1); }
public class QueryH2 : IQueryHandler<DuplicateQuery, int> { public ValueTask<int> Handle(DuplicateQuery q, CancellationToken ct) => new(2); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM003" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Diagnostics_ELM005_OpenGenericHandler_ReportsWarning()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record GenericCommand<T>() : ICommand<T>;
public class OpenGenericCommandHandler<T> : ICommandHandler<GenericCommand<T>, T>
{
    public ValueTask<T> Handle(GenericCommand<T> request, CancellationToken cancellationToken) => default;
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Should().Contain(d => d.Id == "ELM005" && d.Severity == DiagnosticSeverity.Warning && d.GetMessage().Contains("OpenGenericCommandHandler"));
    }

    [Fact]
    public void OpenGenericBehavior_SingleTypeParameter_ConstraintsCheckedCorrectly()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public interface ICustomMarker {}
public class BaseReqClass {}

[UseBehavior(typeof(InterfaceConstrainedBehavior<>))]
[UseBehavior(typeof(BaseClassConstrainedBehavior<>))]
public record SatisfyingCmd() : BaseReqClass, ICommand<int>, ICustomMarker;

public class SatisfyingCmdHandler : ICommandHandler<SatisfyingCmd, int>
{
    public ValueTask<int> Handle(SatisfyingCmd c, CancellationToken ct) => new(42);
}

public class InterfaceConstrainedBehavior<TReq> : IPipelineBehavior<TReq, int>
    where TReq : ICustomMarker
{
    public ValueTask<int> Handle<TNext>(TReq request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<int> => next.InvokeAsync();
}

public class BaseClassConstrainedBehavior<TReq> : IPipelineBehavior<TReq, int>
    where TReq : BaseReqClass
{
    public ValueTask<int> Handle<TNext>(TReq request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<int> => next.InvokeAsync();
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("InterfaceConstrainedBehavior<global::SatisfyingCmd>");
        dispSource.Should().Contain("BaseClassConstrainedBehavior<global::SatisfyingCmd>");
    }

    [Fact]
    public void OpenGenericBehavior_UnsatisfiedConstraint_IsSkipped()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public interface IOtherMarker {}

[UseBehavior(typeof(UnsatisfiedBehavior<>))]
public record PlainCmd() : ICommand<int>;

public class PlainCmdHandler : ICommandHandler<PlainCmd, int>
{
    public ValueTask<int> Handle(PlainCmd c, CancellationToken ct) => new(42);
}

public class UnsatisfiedBehavior<TReq> : IPipelineBehavior<TReq, int>
    where TReq : IOtherMarker
{
    public ValueTask<int> Handle<TNext>(TReq request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<int> => next.InvokeAsync();
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().NotContain("UnsatisfiedBehavior");
    }

    [Fact]
    public void Diagnostics_Descriptors_HaveCorrectDesignCategoryAndEnabledState()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(InvalidSigBehavior))]
public record ErrReq() : ICommand<int>;
public class ErrReqH : ICommandHandler<ErrReq, int> { public ValueTask<int> Handle(ErrReq r, CancellationToken ct) => new(1); }

public class InvalidSigBehavior : IPipelineBehavior<ErrReq, int>
{
    public int Handle(ErrReq r) => 1;
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diag = diagnostics.FirstOrDefault(d => d.Id == "ELM004");
        diag.Should().NotBeNull();
        diag!.Descriptor.Category.Should().Be("Design");
        diag.Descriptor.IsEnabledByDefault.Should().BeTrue();
        diag.Descriptor.Title.ToString().Should().Be("Invalid Behavior Signature");
    }

    [Fact]
    public void Diagnostics_ELM007_NotificationBehavior_WithTwoTypeParameters_ReportsError()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(DoubleNotifBehavior<,>))]
public record CustomNotification() : INotification;
public class CustomNotifHandler : INotificationHandler<CustomNotification>
{
    public ValueTask Handle(CustomNotification n, CancellationToken ct) => default;
}

public class DoubleNotifBehavior<T1, T2>
{
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diag = diagnostics.FirstOrDefault(d => d.Id == "ELM007");
        diag.Should().NotBeNull();
        diag!.Descriptor.Category.Should().Be("Design");
        diag.Descriptor.IsEnabledByDefault.Should().BeTrue();
        diag.Descriptor.Title.ToString().Should().Be("Unsupported Open Generic Behavior");
        diag.GetMessage().Should().Contain("Only 1 is supported (TNotification) for notifications.");
    }

    [Fact]
    public void Diagnostics_ELM008_BehaviorOrderConflict_FullDescriptorVerification()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(G1), Order = 1)]
[UseBehavior(typeof(G2), Order = 1)]
public record ConflictReq() : ICommand<int>;
public class ConflictReqH : ICommandHandler<ConflictReq, int> { public ValueTask<int> Handle(ConflictReq r, CancellationToken ct) => new(1); }

public class G1 : IPipelineBehavior<ConflictReq, int> { public ValueTask<int> Handle<TNext>(ConflictReq r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync(); }
public class G2 : IPipelineBehavior<ConflictReq, int> { public ValueTask<int> Handle<TNext>(ConflictReq r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync(); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diag = diagnostics.FirstOrDefault(d => d.Id == "ELM008");
        diag.Should().NotBeNull();
        diag!.Descriptor.Category.Should().Be("Design");
        diag.Descriptor.IsEnabledByDefault.Should().BeTrue();
        diag.Descriptor.Title.ToString().Should().Be("Behavior Order Conflict");
        diag.GetMessage().Should().Be("Behaviors G1, G2 have the same order (0). The execution order between them is not deterministic.");
    }

    [Fact]
    public void SharedBehavior_AcrossMultipleHandlers_RegisteredOnceInDI()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(SharedBehavior))]
public record ReqA() : ICommand<int>;
public class ReqAH : ICommandHandler<ReqA, int> { public ValueTask<int> Handle(ReqA r, CancellationToken ct) => new(1); }

[UseBehavior(typeof(SharedBehavior))]
public record ReqB() : ICommand<int>;
public class ReqBH : ICommandHandler<ReqB, int> { public ValueTask<int> Handle(ReqB r, CancellationToken ct) => new(2); }

public class SharedBehavior : IPipelineBehavior<ReqA, int>, IPipelineBehavior<ReqB, int>
{
    public ValueTask<int> Handle<TNext>(ReqA r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync();
    public ValueTask<int> Handle<TNext>(ReqB r, TNext n, CancellationToken ct) where TNext : struct, INext<int> => n.InvokeAsync();
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediatorExtensions.g.cs")).ToString();

        // Must contain registration exactly once
        diSource.Should().Contain("services.AddTransient<global::SharedBehavior>();");
    }

    [Fact]
    public void NotificationBehavior_RegisteredInDI()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(ConcreteNotifBehavior))]
public record NotifMsg() : INotification;
public class NotifMsgH : INotificationHandler<NotifMsg> { public ValueTask Handle(NotifMsg n, CancellationToken ct) => default; }

public class ConcreteNotifBehavior : INotificationBehavior<NotifMsg>
{
    public ValueTask Handle<TNext>(NotifMsg notification, TNext next, CancellationToken cancellationToken) where TNext : struct, INext => next.InvokeAsync();
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediatorExtensions.g.cs")).ToString();

        diSource.Should().Contain("services.AddTransient<global::ConcreteNotifBehavior>();");
    }

    [Fact]
    public void Diagnostics_ELM001_UnhandledQuery_ReportsErrorWithFullMetadata()
    {
        var source = @"
using EricksonLopez.Mediator;

public record UnhandledQuery() : IQuery<string>;";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diag = diagnostics.FirstOrDefault(d => d.Id == "ELM001");
        diag.Should().NotBeNull();
        diag!.Descriptor.Category.Should().Be("Design");
        diag.Descriptor.IsEnabledByDefault.Should().BeTrue();
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
        diag.GetMessage().Should().Contain("UnhandledQuery");
    }

    [Fact]
    public void Diagnostics_ELM009_UnhandledStreamRequest_ReportsErrorWithFullMetadata()
    {
        var source = @"
using EricksonLopez.Mediator;

public record UnhandledStream() : IStreamRequest<int>;";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diag = diagnostics.FirstOrDefault(d => d.Id == "ELM009");
        diag.Should().NotBeNull();
        diag!.Descriptor.Category.Should().Be("Design");
        diag.Descriptor.IsEnabledByDefault.Should().BeTrue();
        diag.Descriptor.Title.ToString().Should().Be("Stream Handler Not Found");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
        diag.GetMessage().Should().Be("No stream handler found for UnhandledStream");
    }

    [Fact]
    public void Diagnostics_ELM006_UnhandledNotification_ReportsWarningWithFullMetadata()
    {
        var source = @"
using EricksonLopez.Mediator;

public record OrphanNotification() : INotification;";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diag = diagnostics.FirstOrDefault(d => d.Id == "ELM006");
        diag.Should().NotBeNull();
        diag!.Descriptor.Category.Should().Be("Design");
        diag.Descriptor.IsEnabledByDefault.Should().BeTrue();
        diag.Descriptor.Title.ToString().Should().Be("Notification Handler Not Found");
        diag.Severity.Should().Be(DiagnosticSeverity.Warning);
        diag.GetMessage().Should().Be("No handler found for notification OrphanNotification. It will be ignored when published.");
    }

    [Fact]
    public void Validation_Range_WithDefaultAndCustomMessage_EmitsCorrectChecks()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[ValidateRequest]
public record RangeReq(
    [property: ValidateRange(1, 100)] int Age,
    [property: ValidateRange(10, 50, ErrorMessage = ""Custom range error."")] int Score
) : ICommand<int>;

public class RangeReqH : ICommandHandler<RangeReq, int>
{
    public ValueTask<int> Handle(RangeReq r, CancellationToken ct) => new(r.Age);
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("if (req.Age < 1 || req.Age > 100) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Age must be between 1 and 100.\");");
        dispSource.Should().Contain("if (req.Score < 10 || req.Score > 50) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom range error.\");");
    }

    [Fact]
    public void Validation_Length_WithDefaultAndCustomMessage_EmitsCorrectChecks()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[ValidateRequest]
public record LengthReq(
    [property: ValidateLength(3, 10)] string Code,
    [property: ValidateLength(5, 20, ErrorMessage = ""Custom length error."")] string Name
) : ICommand<int>;

public class LengthReqH : ICommandHandler<LengthReq, int>
{
    public ValueTask<int> Handle(LengthReq r, CancellationToken ct) => new(1);
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("if (req.Code == null || req.Code.Length < 3 || req.Code.Length > 10) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Code length must be between 3 and 10.\");");
        dispSource.Should().Contain("if (req.Name == null || req.Name.Length < 5 || req.Name.Length > 20) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom length error.\");");
    }

    [Fact]
    public void Notification_MultipleBehaviors_GeneratesFullNestedStructPipeline()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[UseBehavior(typeof(NB1))]
[UseBehavior(typeof(NB2))]
public record MultiBehaviorNotif() : INotification;

public class MultiBehaviorNotifH : INotificationHandler<MultiBehaviorNotif>
{
    public ValueTask Handle(MultiBehaviorNotif n, CancellationToken ct) => default;
}

public class NB1 : INotificationBehavior<MultiBehaviorNotif>
{
    public ValueTask Handle<TNext>(MultiBehaviorNotif n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
}

public class NB2 : INotificationBehavior<MultiBehaviorNotif>
{
    public ValueTask Handle<TNext>(MultiBehaviorNotif n, TNext next, CancellationToken ct) where TNext : struct, INext => next.InvokeAsync();
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("MultiBehaviorNotifBehavior1Next");
        dispSource.Should().Contain("MultiBehaviorNotifBehavior0Next");
        dispSource.Should().Contain("MultiBehaviorNotifNotificationNext");
    }

    [Fact]
    public void DependencyInjection_ResultResponse_GeneratesResultFactoryAndRegistration()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Result
{
    public record Error(string Code, string Message);
    public class Result
    {
        public static Result Failure(Error error) => new();
    }
}

public record ResultReq() : ICommand<global::EricksonLopez.Result.Result>;

public class ResultReqH : ICommandHandler<ResultReq, global::EricksonLopez.Result.Result>
{
    public ValueTask<global::EricksonLopez.Result.Result> Handle(ResultReq r, CancellationToken ct) => new(new global::EricksonLopez.Result.Result());
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediatorExtensions.g.cs")).ToString();

        diSource.Should().Contain("services.AddSingleton<global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result>, ResultFactory0>();");
        diSource.Should().Contain("internal sealed class ResultFactory0 : global::EricksonLopez.Mediator.Result.IResultFactory<global::EricksonLopez.Result.Result>");
        diSource.Should().Contain("public global::EricksonLopez.Result.Result CreateFailure(global::EricksonLopez.Result.Error error)");
        diSource.Should().Contain("return global::EricksonLopez.Result.Result.Failure(error);");
    }

    [Fact]
    public void GeneratedMediator_FullStaticAndInterfaceDispatch_EmitsAllRequiredSignatures()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record PingCmd() : ICommand<string>;
public class PingCmdH : ICommandHandler<PingCmd, string> { public ValueTask<string> Handle(PingCmd c, CancellationToken ct) => new(""pong""); }

public record PingQuery() : IQuery<int>;
public class PingQueryH : IQueryHandler<PingQuery, int> { public ValueTask<int> Handle(PingQuery q, CancellationToken ct) => new(1); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("// <auto-generated/>");
        dispSource.Should().Contain("#pragma warning disable CS8600, CS8604, CS1522");
        dispSource.Should().Contain("public sealed class GeneratedMediator : IMediator");
        dispSource.Should().Contain("public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public ValueTask<TResponse> SendCommand<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public ValueTask<TResponse> SendQuery<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public static ValueTask<TResponse> SendCommandStatic<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public static ValueTask<TResponse> SendQueryStatic<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)");
        dispSource.Should().Contain("public static ValueTask PublishStatic<TNotification>(TNotification notification, CancellationToken cancellationToken = default)");
    }

    [Fact]
    public void GeneratedMediatorExtensions_HeaderAndStandardRegistrations_EmitsAllRequiredLines()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

public record SimpleCmd() : ICommand<int>;
public class SimpleCmdH : ICommandHandler<SimpleCmd, int> { public ValueTask<int> Handle(SimpleCmd c, CancellationToken ct) => new(1); }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var diSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediatorExtensions.g.cs")).ToString();

        diSource.Should().Contain("// <auto-generated/>");
        diSource.Should().Contain("using Microsoft.Extensions.DependencyInjection;");
        diSource.Should().Contain("using EricksonLopez.Mediator;");
        diSource.Should().Contain("using EricksonLopez.Mediator.Generated;");
        diSource.Should().Contain("namespace Microsoft.Extensions.DependencyInjection");
        diSource.Should().Contain("public static class GeneratedMediatorExtensions");
        diSource.Should().Contain("public static IServiceCollection AddEricksonLopezMediator(this IServiceCollection services)");
        diSource.Should().Contain("services.AddSingleton<IMediator, GeneratedMediator>();");
        diSource.Should().Contain("services.AddSingleton<ISender>(sp => sp.GetRequiredService<IMediator>());");
        diSource.Should().Contain("services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<IMediator>());");
        diSource.Should().Contain("return services;");
    }

    [Fact]
    public void Notification_SequentialAndParallel_PublishStrategies_EmitCorrectInvocations()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

[PublishStrategy(PublishStrategy.Sequential)]
public record SeqNotif() : INotification;
public class SeqH1 : INotificationHandler<SeqNotif> { public ValueTask Handle(SeqNotif n, CancellationToken ct) => default; }
public class SeqH2 : INotificationHandler<SeqNotif> { public ValueTask Handle(SeqNotif n, CancellationToken ct) => default; }

[PublishStrategy(PublishStrategy.Parallel)]
public record ParNotif() : INotification;
public class ParH1 : INotificationHandler<ParNotif> { public ValueTask Handle(ParNotif n, CancellationToken ct) => default; }
public class ParH2 : INotificationHandler<ParNotif> { public ValueTask Handle(ParNotif n, CancellationToken ct) => default; }

[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public record AggNotif() : INotification;
public class AggH1 : INotificationHandler<AggNotif> { public ValueTask Handle(AggNotif n, CancellationToken ct) => default; }
public class AggH2 : INotificationHandler<AggNotif> { public ValueTask Handle(AggNotif n, CancellationToken ct) => default; }";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var dispSource = driver.GetRunResult().GeneratedTrees.First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs")).ToString();

        dispSource.Should().Contain("await _sp.GetRequiredService<global::SeqH1>().Handle(_n, _ct).ConfigureAwait(false);");
        dispSource.Should().Contain("await _sp.GetRequiredService<global::SeqH2>().Handle(_n, _ct).ConfigureAwait(false);");

        dispSource.Should().Contain("var tasks = new Task[2];");
        dispSource.Should().Contain("tasks[0] = _sp.GetRequiredService<global::ParH1>().Handle(_n, _ct).AsTask();");
        dispSource.Should().Contain("tasks[1] = _sp.GetRequiredService<global::ParH2>().Handle(_n, _ct).AsTask();");
        dispSource.Should().Contain("await Task.WhenAll(tasks).ConfigureAwait(false);");

        dispSource.Should().Contain("List<Exception> exceptions = null;");
        dispSource.Should().Contain("exceptions ??= new List<Exception>();");
        dispSource.Should().Contain("if (exceptions is not null) throw new global::EricksonLopez.Mediator.NotificationHandlerAggregateException(exceptions);");
    }
}
