// Copyright © Erickson Lopez. MIT License.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EricksonLopez.Auditing.Analyzers
{
    /// <summary>
    /// Provides diagnostic analysis to ensure that <c>AuditScope</c> instances are captured in using statements.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AuditScopeUsageAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic rule identifier for uncaptured audit scopes.
        /// </summary>
        public const string DiagnosticId = "AUD001";

        private static readonly LocalizableString _title = "AuditScope must be disposed";
        private static readonly LocalizableString _messageFormat = "The AuditScope returned by '{0}' must be captured in a 'using' statement or declaration to prevent memory and context leaks";
        private static readonly LocalizableString _description = "AuditScope uses AsyncLocal under the hood. Failing to dispose it will leak context into the surrounding asynchronous control flow.";
        private const string _category = "Usage";

        private static readonly DiagnosticDescriptor _rule = new(
            DiagnosticId,
            _title,
            _messageFormat,
            _category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: _description);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            if (context.SemanticModel.GetSymbolInfo(invocationExpr, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
                return;

            if (methodSymbol.Name != "Begin" || methodSymbol.ContainingType?.Name != "AuditScope")
                return;

            // Ensure the result is within a Using statement or declaration
            var isWithinUsing = false;

            var current = invocationExpr.Parent;
            while (current != null)
            {
                if (current is UsingStatementSyntax)
                {
                    isWithinUsing = true;
                    break;
                }

                if (current is LocalDeclarationStatementSyntax localDecl && localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
                {
                    isWithinUsing = true;
                    break;
                }

                // If it is just an assignment, we should check if the variable being assigned to is ever disposed.
                // But a simple heuristic is: if it's not a using statement/declaration immediately or casted, warn.
                if (current is ExpressionStatementSyntax || current is ReturnStatementSyntax || current is ArrowExpressionClauseSyntax)
                {
                    break;
                }

                current = current.Parent;
            }

            if (!isWithinUsing)
            {
                var diagnostic = Diagnostic.Create(_rule, invocationExpr.GetLocation(), methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
