// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace EricksonLopez.Auditing.Analyzers.Tests;

public sealed class AuditScopeUsageAnalyzerTests
{
    private const string _auditScopeStub = @"
namespace EricksonLopez.Auditing
{
    public sealed class AuditScope : System.IDisposable
    {
        public static AuditScope Begin() => new AuditScope();
        public static void OtherMethod() { }
        public void Dispose() { }
    }
}
";

    [Fact]
    public void SupportedDiagnostics_RuleConfiguration_MatchesEnterpriseQualityStandards()
    {
        var analyzer = new AuditScopeUsageAnalyzer();
        Assert.Single(analyzer.SupportedDiagnostics);

        var rule = analyzer.SupportedDiagnostics[0];
        Assert.Equal(AuditScopeUsageAnalyzer.DiagnosticId, rule.Id);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Warning, rule.DefaultSeverity);
        Assert.Equal("Usage", rule.Category);
        Assert.Equal("AuditScope must be disposed", rule.Title.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains("must be captured in a 'using' statement or declaration", rule.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("AuditScope uses AsyncLocal under the hood", rule.Description.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginWithoutUsing_EmitsWarningDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public void Run()
        {
            {|#0:AuditScope.Begin()|};
        }
    }
}
" + _auditScopeStub;

        var expected = new DiagnosticResult(AuditScopeUsageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("Begin");

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginInArrowExpression_EmitsWarningDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public AuditScope GetScope() => {|#0:AuditScope.Begin()|};
    }
}
" + _auditScopeStub;

        var expected = new DiagnosticResult(AuditScopeUsageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("Begin");

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginInReturnStatement_EmitsWarningDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public AuditScope GetScope()
        {
            return {|#0:AuditScope.Begin()|};
        }
    }
}
" + _auditScopeStub;

        var expected = new DiagnosticResult(AuditScopeUsageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("Begin");

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginWithUsingStatement_DoesNotEmitDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public void Run()
        {
            using (var scope = AuditScope.Begin())
            {
            }
        }
    }
}
" + _auditScopeStub;

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginWithUsingDeclaration_DoesNotEmitDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public void Run()
        {
            using var scope = AuditScope.Begin();
        }
    }
}
" + _auditScopeStub;

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginNestedInUsingStatement_DoesNotEmitDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public void Run()
        {
            using (var outer = AuditScope.Begin())
            {
                using (var inner = AuditScope.Begin())
                {
                }
            }
        }
    }
}
" + _auditScopeStub;

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AnalyzeNode_AuditScopeBeginNestedInUsingDeclaration_DoesNotEmitDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public void Run()
        {
            using var scope1 = AuditScope.Begin();
            using var scope2 = AuditScope.Begin();
        }
    }
}
" + _auditScopeStub;

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AnalyzeNode_UnrelatedMethodInvocation_DoesNotEmitDiagnostic()
    {
        var testCode = @"
using EricksonLopez.Auditing;

namespace TestApp
{
    public class SampleClass
    {
        public void Run()
        {
            AuditScope.OtherMethod();
        }
    }
}
" + _auditScopeStub;

        await CSharpAnalyzerVerifier<AuditScopeUsageAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(testCode);
    }
}
