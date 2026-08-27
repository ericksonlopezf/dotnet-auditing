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
    public void TwoArg_ValidateRange_And_ValidateLength_GeneratesExactComparisonWithoutMessage()
    {
        string source = @"

namespace TestApp
{
    public class RangeAndLengthCmd : ICommand<int>
    {
        [ValidateRange(5, 50)]
        public int Age { get; set; }

        [ValidateLength(3, 30)]
        public string Name { get; set; }
    }

    public class RangeAndLengthCmdHandler : ICommandHandler<RangeAndLengthCmd, int>
    {
        public ValueTask<int> Handle(RangeAndLengthCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("if (req.Age < 5 || req.Age > 50) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Age must be between 5 and 50.\");");
        dispatcherCode.Should().Contain("if (req.Name == null || req.Name.Length < 3 || req.Name.Length > 30) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Name length must be between 3 and 30.\");");
    }

    [Fact]
    public void ZeroArg_ValidateNotNull_WithNoArguments_GeneratesNullCheck()
    {
        string source = @"

namespace TestApp
{
    public class NotNullCmd : ICommand<int>
    {
        [ValidateNotNull]
        public string Data { get; set; }
    }

    public class NotNullCmdHandler : ICommandHandler<NotNullCmd, int>
    {
        public ValueTask<int> Handle(NotNullCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("if (req.Data == null) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Data must not be null.\");");
    }

    [Fact]
    public void ValidationAttribute_WithNamedErrorMessageProperty_GeneratesExactCustomMessage()
    {
        string source = @"

namespace TestApp
{
    public class NamedErrorCmd : ICommand<int>
    {
        [ValidateNotNull(ErrorMessage = ""Custom named error message."")]
        public string Title { get; set; }
    }

    public class NamedErrorCmdHandler : ICommandHandler<NamedErrorCmd, int>
    {
        public ValueTask<int> Handle(NamedErrorCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("if (req.Title == null) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom named error message.\");");
    }

    [Fact]
    public void DispatcherGenerator_RegexWithQuotes_EscapesQuotesCorrectly()
    {
        string source = @"

namespace TestApp
{
    [ValidateRequest]
    public class RegexCmd : ICommand<int>
    {
        [ValidateRegex(@""a""""b"", ErrorMessage = ""Quotes regex msg"")]
        public string Text { get; set; }
    }

    public class RegexCmdHandler : ICommandHandler<RegexCmd, int>
    {
        public ValueTask<int> Handle(RegexCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("if (req.Text == null || !global::System.Text.RegularExpressions.Regex.IsMatch(req.Text, \"a\\\"b\", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(2))) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Quotes regex msg\");", dispatcherCode);
    }

    [Fact]
    public void ValidationAttributes_AllTypes_WithConstructorsAndNamedProperties()
    {
        string source = @"

namespace TestApp
{
    [ValidateRequest]
    public class ValidatedCmd : ICommand<int>
    {
        [ValidateNotNull]
        public string Prop1 { get; set; }

        [ValidateNotEmpty(ErrorMessage = ""Must not be empty"")]
        public string Prop2 { get; set; }

        [ValidateRange(10.5, 99.5)]
        public double Prop3 { get; set; }

        [ValidateRange(1.0, 5.0, ErrorMessage = ""Range custom"")]
        public double Prop4 { get; set; }

        [ValidateLength(3, 10)]
        public string Prop5 { get; set; }

        [ValidateLength(2, 8, ErrorMessage = ""Length custom"")]
        public string Prop6 { get; set; }

        [ValidateRegex(@""^[A-Z]+$"")]
        public string Prop7 { get; set; }

        [ValidateRegex(@""^[0-9]+$"", ErrorMessage = ""Digits only"")]
        public string Prop8 { get; set; }
    }

    public class ValidatedCmdHandler : ICommandHandler<ValidatedCmd, int>
    {
        public ValueTask<int> Handle(ValidatedCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("Prop1 must not be null.");
        dispatcherCode.Should().Contain("Must not be empty");
        dispatcherCode.Should().Contain("Prop3 must be between 10.5 and 99.5.");
        dispatcherCode.Should().Contain("Range custom");
        dispatcherCode.Should().Contain("Prop5 length must be between 3 and 10.");
        dispatcherCode.Should().Contain("Length custom");
        dispatcherCode.Should().Contain("Prop7 must match pattern '^[A-Z]+$'.");
        dispatcherCode.Should().Contain("Digits only");
    }

    [Fact]
    public void ValidationAttribute_RangeAndLength_WithConstructorArguments_EmitsMinMaxInValidation()
    {
        string source = @"

namespace TestApp
{
    [ValidateRequest]
    public class RangeLenCmd : ICommand<int>
    {
        [ValidateRange(5.5, 20.5, ""Range error"")]
        public double Score { get; set; }

        [ValidateLength(4, 12, ""Length error"")]
        public string Code { get; set; }

        [ValidateRegex(@""^[0-9]+$"", ""Regex error"")]
        public string Num { get; set; }
    }

    public class RangeLenCmdHandler : ICommandHandler<RangeLenCmd, int>
    {
        public ValueTask<int> Handle(RangeLenCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        dispatcherCode.Should().Contain("if (req.Score < 5.5 || req.Score > 20.5) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Range error\");");
        dispatcherCode.Should().Contain("if (req.Code == null || req.Code.Length < 4 || req.Code.Length > 12) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Length error\");");
        Assert.Contains("if (req.Num == null || !global::System.Text.RegularExpressions.Regex.IsMatch(req.Num, \"^[0-9]+$\", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(2))) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Regex error\");", dispatcherCode);
    }

    [Fact]
    public void ValidationAttribute_RegexWithNullPattern_EmitsEmptyRegex()
    {
        string source = @"

namespace TestApp
{
    [ValidateRequest]
    public class NullRegexCmd : ICommand<int>
    {
        [ValidateRegex(null)]
        public string Text { get; set; }
    }

    public class NullRegexCmdHandler : ICommandHandler<NullRegexCmd, int>
    {
        public ValueTask<int> Handle(NullRegexCmd cmd, CancellationToken ct) => new(1);
    }
}";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outComp, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var dispatcherCode = outComp.SyntaxTrees
            .First(t => t.FilePath.Contains("GeneratedMediator.g.cs")).ToString();

        Assert.Contains("!global::System.Text.RegularExpressions.Regex.IsMatch(req.Text, \"\", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(2))", dispatcherCode);
    }

}

