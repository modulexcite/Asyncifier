using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Text;
using RoslynUtilities;
using System;

namespace Asyncifier
{
    [ExportCodeRefactoringProvider(AsyncifierCodeRefactoringProvider.RefactoringId, LanguageNames.CSharp), Shared]
    internal class AsyncifierCodeRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = "Asyncifier";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

            var root = (CompilationUnitSyntax) await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
           
            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            // Only offer a refactoring if the selected node is a type declaration node.
            var identifier = node as IdentifierNameSyntax;
            if (identifier == null || !identifier.Identifier.Text.StartsWith("Begin") 
                || identifier.Parent == null 
                || identifier.Parent.CSharpKind() != SyntaxKind.SimpleMemberAccessExpression 
                || identifier.Parent.Parent == null
                || identifier.Parent.Parent.CSharpKind() != SyntaxKind.InvocationExpression)
            {
                return;
            }

            var invocationExpr = (InvocationExpressionSyntax) identifier.Parent.Parent;

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var invocationSymbol = semanticModel.GetSymbolInfo(invocationExpr, context.CancellationToken).Symbol as IMethodSymbol;

            if (invocationSymbol == null || !invocationSymbol.IsAPMBeginMethod())
            {
                return;
            }


            // For any type declaration node, create a code action to reverse the identifier text.
            var action = CodeAction.Create("Make it async", c => RefactorAPMToAsyncAwait(context.Document, semanticModel, root, invocationExpr, invocationSymbol, c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        public static ArgumentSyntax FindAsyncCallbackInvocationArgument(InvocationExpressionSyntax apmSyntax, IMethodSymbol apmSymbol)
        {
            const string parameterTypeName = "System.AsyncCallback";

            var parameterIndex = FindMethodParameterIndex(apmSymbol, parameterTypeName);
            var callbackArgument = apmSyntax.ArgumentList.Arguments.ElementAt(parameterIndex);

            return callbackArgument;
        }

        public static ArgumentSyntax FindAsyncStateInvocationArgument(InvocationExpressionSyntax apmSyntax, IMethodSymbol apmSymbol)
        {
            var parameterIndex = FindMethodParameterIndex(apmSymbol, "object", "state");
            var callbackArgument = apmSyntax.ArgumentList.Arguments.ElementAt(parameterIndex);

            return callbackArgument;
        }

        public static CompilationUnitSyntax RefactorSimpleLambdaInstance(CompilationUnitSyntax syntax, InvocationExpressionSyntax apmSyntax, IMethodSymbol apmSymbol, SimpleLambdaExpressionSyntax lambda)
        {
            if (lambda.Body.CSharpKind() != SyntaxKind.Block)
                throw new NotImplementedException("Lambda body must be rewritten as BlockSyntax - it is now: " + lambda.Body.CSharpKind() + ": lambda: " + lambda);

            var lambdaBlock = (BlockSyntax)lambda.Body;

            var originatingMethodSyntax = apmSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            // TODO: This precondition'i Analyzer'a koy!!! 
            if (originatingMethodSyntax == null || !originatingMethodSyntax.ReturnsVoid())
            {
                throw new Exception("PRECONDITION: Initiating method does not return void");
            }

            // TODO: Look up the symbol to check that it actually exists.
            var methodNameBase = GetAsyncMethodNameBase(apmSyntax);

            var endStatement = TryFindEndAPMCallSyntaxNode(lambdaBlock, methodNameBase);

            if (endStatement != null)
            {
                return RewriteNotNestedInstance(syntax, originatingMethodSyntax, apmSyntax, lambdaBlock, endStatement, methodNameBase, );
            }

            // Every method invocation might lead to the target EndXxx. Try to find it recursively.
            // Once found, rewrite the methods in the invocation path, one by one.
            // Finally, rewrite the originating method, and the method with the EndXxx statement.

            var invocationPathToEndXxx = TryFindCallGraphPathToEndXxx(lambdaBlock, methodNameBase, model);

            if (invocationPathToEndXxx.Count == 0)
            {
                throw new PreconditionException("Could not find End call in lambda body call graph");
            }

            // These two get special treatment.
            var initialCall = invocationPathToEndXxx.RemoveLast();
            var endXxxCall = invocationPathToEndXxx.RemoveFirst();

            IMethodSymbol endXxxMethod;
            try
            {
                endXxxMethod = model.LookupMethodSymbol(endXxxCall);
            }
            catch (SymbolMissingException e)
            {
                Logger.Error("No symbol found for APM End invocation: {0}", endXxxCall, e);

                throw new RefactoringException("No symbol found for APM End invocation: " + endXxxCall, e);
            }

            var taskTypeParameter = endXxxMethod.ReturnType.Name;

            var replacements = new List<SyntaxReplacementPair>(invocationPathToEndXxx.Count + 2);

            // Replace all intermediate methods on the call graph path.
            replacements.AddRange(
                invocationPathToEndXxx.Select(
                    invocation => new SyntaxReplacementPair(
                        invocation.ContainingMethod(),
                        RewriteCallGraphPathComponent(invocation, taskTypeParameter)
                    )
                )
            );

            // Replace method that contains BeginXxx call.
            var taskName = FreeTaskName(originatingMethodSyntax);
            replacements.Add(
                new SyntaxReplacementPair(
                    originatingMethodSyntax,
                    RewriteOriginatingMethod(
                        apmSyntax,
                        RewriteOriginatingMethodLambdaBlock(lambda, initialCall, taskName),
                        methodNameBase,
                        taskName
                    )
                )
            );

            // Replace method that contains the EndXxx call.
            replacements.Add(
                new SyntaxReplacementPair(
                    endXxxCall.ContainingMethod(),
                    RewriteEndXxxContainingMethod(
                        endXxxCall,
                        taskTypeParameter
                    )
                )
            );

            return syntax
                .ReplaceAll(replacements)
                .WithUsingSystemThreadingTasks()
                .Format(workspace);
        }

        private static CompilationUnitSyntax RewriteNotNestedInstance(CompilationUnitSyntax root, MethodDeclarationSyntax originalCallingMethod, InvocationExpressionSyntax apmSyntax, BlockSyntax lambdaBlock, InvocationExpressionSyntax endStatement, string methodNameBase, Workspace workspace)
        {
            var taskName = FreeTaskName(originalCallingMethod);

            var awaitStatement = NewAwaitExpression(taskName);
            var rewrittenLambdaBlock = lambdaBlock.ReplaceNode(endStatement, awaitStatement);

            var newCallingMethod = RewriteOriginatingMethod(beginXxxCall, rewrittenLambdaBlock, methodNameBase, taskName);

            return root
                .ReplaceNode(originalCallingMethod, newCallingMethod)
                .WithUsingSystemThreadingTasks()
                .Format(workspace);
        }



        #region Utilities

        private const string DefaultTaskName = "task";
        private const string DefaultLambdaParamName = "result";

        private static string FreeTaskName(MethodDeclarationSyntax syntax)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");

            return FindFreeIdentifier(syntax, DefaultTaskName);
        }

        private static string FindFreeIdentifier(MethodDeclarationSyntax syntax, string name)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");

            var union = DeclaredIdentifiers(syntax).ToArray();

            if (!union.Contains(name))
                return name;

            for (var i = 2; i < 10; i++)
            {
                var freeName = name + i;

                if (!union.Contains(freeName))
                    return freeName;
            }

            throw new RefactoringException("Tried name suffixed with 2-10 - all already in use: " + name);
        }

        private static IEnumerable<string> DeclaredIdentifiers(MethodDeclarationSyntax syntax)
        {
            var methodParameterNames = syntax.ParameterList.Parameters
                .Select(p => p.Identifier.ValueText);

            var methodLocalVars = syntax
                .DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .SelectMany(d => d.Declaration.Variables)
                .Select(v => v.Identifier.ValueText);

            var classFieldIds = syntax.ContainingClass()
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Select(v => v.Identifier.ValueText);

            var classPropertyIds = syntax.ContainingClass()
                .DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Select(p => p.Identifier.ValueText);

            var classMethodIds = syntax.ContainingClass()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(m => m.Identifier.ValueText);

            var classDelegateIds = syntax.ContainingClass()
                .DescendantNodes()
                .OfType<DelegateDeclarationSyntax>()
                .Select(d => d.Identifier.ValueText);

            return methodParameterNames
                .Concat(methodLocalVars)
                .Concat(classFieldIds)
                .Concat(classPropertyIds)
                .Concat(classMethodIds)
                .Concat(classDelegateIds);
        }

        private static InvocationExpressionSyntax TryFindEndAPMCallSyntaxNode(BlockSyntax lambdaBlock, string methodNameBase)
        {
            // TODO: Check for correct signature, etc.
            // This can be done much smarter by e.g. using the BeginXxx method symbol, looking up the corresponding EndXxx symobl, and filtering on that.

            try
            {
                // TODO: Also considier IdentifierName EndXxx instances.
                var endXxxExpression = lambdaBlock.DescendantNodes()
                                                  .OfType<MemberAccessExpressionSyntax>()
                                                  .Where(node => NodeIsNotContainedInLambdaExpression(node, lambdaBlock))
                                                  .First(stmt => stmt.Name.ToString().Equals("End" + methodNameBase));

                return (InvocationExpressionSyntax)endXxxExpression.Parent;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Check that the path from the given node to the top node does not contain a simple or parenthesized lambda expression.
        ///
        /// Note: when topNode is not an ancestor of node, behavior is undefined.
        /// </summary>
        /// <param name="node">Node to check</param>
        /// <param name="topNode">Top level node to check the path to</param>
        /// <returns>true if the node is not contained in a lambda expression</returns>
        private static bool NodeIsNotContainedInLambdaExpression(SyntaxNode node, SyntaxNode topNode)
        {
            while (node != null && node != topNode)
            {
                if (node.CSharpKind() == SyntaxKind.SimpleLambdaExpression ||
                    node.CSharpKind() == SyntaxKind.ParenthesizedLambdaExpression)
                {
                    return false;
                }

                node = node.Parent;
            }

            return true;
        }

        private static string GetAsyncMethodNameBase(InvocationExpressionSyntax invocation)
        {
            var expression = (MemberAccessExpressionSyntax)invocation.Expression;

            var apmMethodName = expression.Name.ToString();
            var methodNameBase = apmMethodName.Substring(5);
            return methodNameBase;
        }

        private static int FindMethodParameterIndex(IMethodSymbol symbol, string typeName)
        {
            for (var i = 0; i < symbol.Parameters.Count(); i++)
            {
                var parameter = symbol.Parameters.ElementAt(i);
                if (parameter.Type.ToDisplayString().Equals(typeName))
                {
                    return i;
                }
            }

            throw new Exception("No " + typeName + " parameter found for method symbol: " + symbol);
        }

        private static int FindMethodParameterIndex(IMethodSymbol symbol, string typeName, string identifierName)
        {
            for (var i = 0; i < symbol.Parameters.Count(); i++)
            {
                var parameter = symbol.Parameters.ElementAt(i);
                if (parameter.Type.ToDisplayString().Equals(typeName) && parameter.Name.Equals(identifierName))
                {
                    return i;
                }
            }

            throw new Exception("No parameter '" + typeName + " " + identifierName + "' found for method symbol: " + symbol);
        }

        #endregion
    }
}