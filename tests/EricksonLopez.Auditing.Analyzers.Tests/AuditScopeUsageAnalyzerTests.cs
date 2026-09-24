// Copyright © Erickson Lopez. MIT License.
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
