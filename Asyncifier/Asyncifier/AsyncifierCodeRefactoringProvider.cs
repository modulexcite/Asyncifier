using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynUtilities;

namespace Asyncifier
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AsyncifierCodeRefactoringProvider)), Shared]
    internal class AsyncifierCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var node = root.FindNode(context.Span);

            var identifier = node as IdentifierNameSyntax;
            if (identifier == null)
            {
                return;
            }

            if (!identifier.Identifier.ValueText.StartsWith("Begin"))
            {
                return;
            }

            var simpleMember = identifier.Parent as MemberAccessExpressionSyntax;
            if (simpleMember == null)
            {
                return;
            }

            var invocation = simpleMember.Parent as InvocationExpressionSyntax;
            if (invocation == null)
            {
                return;
            }

            var cmUnit = root as CompilationUnitSyntax;
            if (cmUnit == null)
            {
                return;
            }

            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            if (model == null)
            {
                return;
            }

            var symbolInfo = model.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol == null)
            {
                return;
            }

            var symbol = symbolInfo.Symbol as IMethodSymbol;
            if (symbol == null || !symbol.IsAPMBeginMethod())
            {
                return;
            }



            var action = CodeAction.Create("Refactor to async/await", c => context.Document.RefactorAPMToAsyncAwait(cmUnit, invocation, c));
            context.RegisterRefactoring(action);
        }
    }
}