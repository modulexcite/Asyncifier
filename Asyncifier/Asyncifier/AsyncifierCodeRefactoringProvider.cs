using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Asyncifier
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AsyncifierCodeRefactoringProvider)), Shared]
    internal class AsyncifierCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var node = root.FindNode(context.Span);

            var invocationExpr = node as InvocationExpressionSyntax;
            if (invocationExpr == null)
            {
                return;
            }

            var cmUnit = root as CompilationUnitSyntax;
            if (cmUnit == null)
            {
                return;
            }

            var action = CodeAction.Create("Refactor to async/await", c => context.Document.RefactorAPMToAsyncAwait(cmUnit, invocationExpr, c));
            context.RegisterRefactoring(action);
        }
    }
}