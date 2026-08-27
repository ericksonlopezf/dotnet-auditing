// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class ValidationGeneratorTests
{
    private static Compilation CreateCompilation(string source) => RoslynTestHelper.CreateCompilation(source, "TestValidationComp");

    [Fact]
    public void Generator_WithValidateNotNull_DefaultAndCustomMessages()
    {
        string source = @"
using EricksonLopez.Mediator;

namespace TestValidationApp
{
    [ValidateRequest]
    public class ValidateNotNullCommand : ICommand<int>
    {
        [ValidateNotNull]
        public object? DefaultNullProp { get; set; }

        [ValidateNotNullAttribute(""Custom null msg"")]
        public string? CustomNullProp { get; set; }
    }

    public class ValidateNotNullHandler : ICommandHandler<ValidateNotNullCommand, int>
    {
        public ValueTask<int> Handle(ValidateNotNullCommand command, CancellationToken cancellationToken) => new(1);
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var runDriver = driver.RunGenerators(compilation);
        var runResult = runDriver.GetRunResult();

        Assert.Empty(runResult.Diagnostics);
        var generatedSource = runResult.GeneratedTrees
            .First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("if (req.DefaultNullProp == null) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"DefaultNullProp must not be null.\");", generatedSource);
        Assert.Contains("if (req.CustomNullProp == null) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom null msg\");", generatedSource);
    }

    [Fact]
    public void Generator_WithValidateNotEmpty_StringGuidAndCollection()
    {
        string source = @"

namespace TestValidationApp
{
    [ValidateRequest]
    public class ValidateNotEmptyCommand : ICommand<int>
    {
        [ValidateNotEmpty]
        public string DefaultString { get; set; } = string.Empty;

        [ValidateNotEmptyAttribute(ErrorMessage = ""Custom string empty"")]
        public string? NullableString { get; set; }

        [ValidateNotEmpty]
        public Guid DefaultGuid { get; set; }

        [ValidateNotEmpty(""Custom guid empty"")]
        public Guid CustomGuid { get; set; }

        [ValidateNotEmpty]
        public List<int> DefaultItems { get; set; } = new();

        [ValidateNotEmpty(""Custom collection empty"")]
        public IEnumerable<string>? CustomItems { get; set; }
    }

    public class ValidateNotEmptyHandler : ICommandHandler<ValidateNotEmptyCommand, int>
    {
        public ValueTask<int> Handle(ValidateNotEmptyCommand command, CancellationToken cancellationToken) => new(1);
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var runDriver = driver.RunGenerators(compilation);
        var runResult = runDriver.GetRunResult();

        Assert.Empty(runResult.Diagnostics);
        var generatedSource = runResult.GeneratedTrees
            .First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("if (string.IsNullOrEmpty(req.DefaultString)) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"DefaultString must not be empty.\");", generatedSource);
        Assert.Contains("if (string.IsNullOrEmpty(req.NullableString)) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom string empty\");", generatedSource);
        Assert.Contains("if (req.DefaultGuid == global::Guid.Empty) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"DefaultGuid must not be empty.\");", generatedSource);
        Assert.Contains("if (req.CustomGuid == global::Guid.Empty) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom guid empty\");", generatedSource);
        Assert.Contains("if (req.DefaultItems == null || !global::Enumerable.Any(req.DefaultItems)) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"DefaultItems must not be empty.\");", generatedSource);
        Assert.Contains("if (req.CustomItems == null || !global::Enumerable.Any(req.CustomItems)) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom collection empty\");", generatedSource);
    }

    [Fact]
    public void Generator_WithValidateRange_DoubleIntAndLong()
    {
        string source = @"

namespace TestValidationApp
{
    [ValidateRequest]
    public class ValidateRangeCommand : ICommand<int>
    {
        [ValidateRange(10, 100)]
        public int IntVal { get; set; }

        [ValidateRange(1.5, 99.5, ""Custom double range"")]
        public double DoubleVal { get; set; }

        [ValidateRange(1000L, 5000L, ErrorMessage = ""Custom long range"")]
        public long LongVal { get; set; }
    }

    public class ValidateRangeHandler : ICommandHandler<ValidateRangeCommand, int>
    {
        public ValueTask<int> Handle(ValidateRangeCommand command, CancellationToken cancellationToken) => new(1);
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var runDriver = driver.RunGenerators(compilation);
        var runResult = runDriver.GetRunResult();

        Assert.Empty(runResult.Diagnostics);
        var generatedSource = runResult.GeneratedTrees
            .First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("if (req.IntVal < 10 || req.IntVal > 100) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"IntVal must be between 10 and 100.\");", generatedSource);
        Assert.Contains("if (req.DoubleVal < 1.5 || req.DoubleVal > 99.5) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom double range\");", generatedSource);
        Assert.Contains("if (req.LongVal < 1000 || req.LongVal > 5000) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom long range\");", generatedSource);
    }

    [Fact]
    public void Generator_WithValidateLength_DefaultAndCustomMessages()
    {
        string source = @"

namespace TestValidationApp
{
    [ValidateRequest]
    public class ValidateLengthCommand : ICommand<int>
    {
        [ValidateLength(3, 50)]
        public string DefaultName { get; set; } = string.Empty;

        [ValidateLengthAttribute(1, 10, ""Custom length msg"")]
        public string CustomName { get; set; } = string.Empty;
    }

    public class ValidateLengthHandler : ICommandHandler<ValidateLengthCommand, int>
    {
        public ValueTask<int> Handle(ValidateLengthCommand command, CancellationToken cancellationToken) => new(1);
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var runDriver = driver.RunGenerators(compilation);
        var runResult = runDriver.GetRunResult();

        Assert.Empty(runResult.Diagnostics);
        var generatedSource = runResult.GeneratedTrees
            .First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("if (req.DefaultName == null || req.DefaultName.Length < 3 || req.DefaultName.Length > 50) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"DefaultName length must be between 3 and 50.\");", generatedSource);
        Assert.Contains("if (req.CustomName == null || req.CustomName.Length < 1 || req.CustomName.Length > 10) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom length msg\");", generatedSource);
    }

    [Fact]
    public void Generator_WithValidateRegex_DefaultAndCustomMessagesWithEscaping()
    {
        string source = @"

namespace TestValidationApp
{
    [ValidateRequest]
    public class ValidateRegexCommand : ICommand<int>
    {
        [ValidateRegex(@""^[A-Z0-9]+$"")]
        public string DefaultCode { get; set; } = string.Empty;

        [ValidateRegexAttribute(@""^[a-z]+$"", ""Custom regex msg"")]
        public string CustomCode { get; set; } = string.Empty;

        [ValidateRegex(@""a""""b"", ErrorMessage = ""Quotes regex msg"")]
        public string QuotesCode { get; set; } = string.Empty;
    }

    public class ValidateRegexHandler : ICommandHandler<ValidateRegexCommand, int>
    {
        public ValueTask<int> Handle(ValidateRegexCommand command, CancellationToken cancellationToken) => new(1);
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var runDriver = driver.RunGenerators(compilation);
        var runResult = runDriver.GetRunResult();

        Assert.Empty(runResult.Diagnostics);
        var generatedSource = runResult.GeneratedTrees
            .First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("if (req.DefaultCode == null || !global::System.Text.RegularExpressions.Regex.IsMatch(req.DefaultCode, \"^[A-Z0-9]+$\", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(2))) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"DefaultCode must match pattern '^[A-Z0-9]+$'.\");", generatedSource);
        Assert.Contains("if (req.CustomCode == null || !global::System.Text.RegularExpressions.Regex.IsMatch(req.CustomCode, \"^[a-z]+$\", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(2))) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Custom regex msg\");", generatedSource);
        Assert.Contains("if (req.QuotesCode == null || !global::System.Text.RegularExpressions.Regex.IsMatch(req.QuotesCode, \"a\\\"b\", global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(2))) throw new global::EricksonLopez.Mediator.MediatorValidationException(\"Quotes regex msg\");", generatedSource);
    }

    [Fact]
    public void Generator_WithoutValidationAttributes_NoValidationEmitted()
    {
        string source = @"

namespace TestValidationApp
{
    public class PlainCommand : ICommand<int>
    {
        public string Name { get; set; } = string.Empty;
    }

    public class PlainCommandHandler : ICommandHandler<PlainCommand, int>
    {
        public ValueTask<int> Handle(PlainCommand command, CancellationToken cancellationToken) => new(1);
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var runDriver = driver.RunGenerators(compilation);
        var runResult = runDriver.GetRunResult();

        Assert.Empty(runResult.Diagnostics);
        var generatedSource = runResult.GeneratedTrees
            .First(t => t.FilePath.EndsWith("GeneratedMediator.g.cs"))
            .GetText()
            .ToString();

        Assert.DoesNotContain("MediatorValidationException", generatedSource);
    }
}



