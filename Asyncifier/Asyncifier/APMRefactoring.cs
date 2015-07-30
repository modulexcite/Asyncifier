using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Formatting;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace Asyncifier
{
    public static class APMToAsyncAwait
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private const string DefaultTaskName = "task";
        private const string DefaultLambdaParamName = "result";
        private const string apmAnnotation = "RefactorableAPMInstance";

        /// <summary>
        /// Execute the APM-to-async/await refactoring for a given APM method invocation.
        /// </summary>
        /// <param name="document">The C# Document on which to operate/in which the Begin and End method calls are represented.</param>
        /// <param name="solution">The solution that contains the C# document.</param>
        /// <param name="workspace">The workspace to which the code in the syntax tree currently belongs, for formatting purposes.</param>
        /// <param name="index">The index number </param>
        /// <returns>The CompilationUnitSyntax node that is the result of the transformation.</returns>
        public static async Task<Document> RefactorAPMToAsyncAwait(this Document document, CompilationUnitSyntax root, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            string message;
            CompilationUnitSyntax rewrittenSyntax;

            //var apmInvocation = document.GetAnnotatedInvocation();
            //if (apmInvocation == null)
            //{
            //    return document;
            //}

            var apmInvocation = invocation.WithAdditionalAnnotations(new SyntaxAnnotation(apmAnnotation));
            rewrittenSyntax = root.ReplaceNode(invocation, apmInvocation);

            while (true)
            {
                document = document.WithSyntaxRoot(rewrittenSyntax);
                rewrittenSyntax = await document.GetSyntaxRootAsync() as CompilationUnitSyntax;
                if (rewrittenSyntax == null)
                {
                    return document;
                }

                apmInvocation = rewrittenSyntax.GetAnnotatedNodes(apmAnnotation).OfType<InvocationExpressionSyntax>().FirstOrDefault();
                if (apmInvocation == null)
                {
                    return document;
                }

                var model = await document.GetSemanticModelAsync(cancellationToken);
                if (model == null)
                {
                    return document;
                }

                var symbolInfo = model.GetSymbolInfo(apmInvocation, cancellationToken);
                if (symbolInfo.Symbol == null)
                {
                    return document;
                }

                var apmSymbol = symbolInfo.Symbol as IMethodSymbol;
                if (apmSymbol == null)
                {
                    return document;
                }

                var callbackArgument = FindAsyncCallbackInvocationArgument(apmInvocation, apmSymbol);
                var callbackExpression = callbackArgument.Expression;

                switch (callbackExpression.Kind())
                {
                    case SyntaxKind.SimpleLambdaExpression:
                        var lambda = (SimpleLambdaExpressionSyntax)callbackExpression;

                        switch (lambda.Body.Kind())
                        {
                            case SyntaxKind.Block:
                                var stateArgument = FindAsyncStateInvocationArgument(apmInvocation, apmSymbol);
                                switch (stateArgument.Expression.Kind())
                                {
                                    case SyntaxKind.NullLiteralExpression:
                                        Logger.Info("Refactoring:\n{0}", apmInvocation.ContainingMethod());
                                        rewrittenSyntax = RefactorSimpleLambdaInstance(rewrittenSyntax, apmInvocation, apmSymbol, model, callbackArgument).WithAdditionalAnnotations(Formatter.Annotation);
                                        return document.WithSyntaxRoot(rewrittenSyntax);

                                    default:
                                        Logger.Info("Rewriting to remove state argument:\n{0}", apmInvocation);
                                        rewrittenSyntax = RewriteStateArgumentToNull(lambda, rewrittenSyntax, stateArgument);
                                        break;
                                }

                                break;

                            case SyntaxKind.InvocationExpression:
                                Logger.Info("Rewriting lambda to block form:\n{0}", apmInvocation);
                                rewrittenSyntax = RewriteInvocationExpressionToBlock(rewrittenSyntax, lambda, model, apmInvocation, apmSymbol);
                                break;

                            default:
                                message = String.Format(
                                        "Unsupported lambda body kind: {0}: method:\n{1}",
                                        lambda.Body.Kind(),
                                        apmInvocation.ContainingMethod()
                                    );
                                Logger.Error("Not implemented: {0}", message);
                                return document;
                        }

                        break;

                    case SyntaxKind.IdentifierName:
                    case SyntaxKind.SimpleMemberAccessExpression:
                        Logger.Info("Rewriting method reference to lambda:\n{0}", apmInvocation);
                        rewrittenSyntax = RewriteMethodReferenceToSimpleLambda(rewrittenSyntax, apmInvocation, apmSymbol, model, callbackArgument, callbackExpression);
                        break;

                    case SyntaxKind.ParenthesizedLambdaExpression:
                        Logger.Info("Rewriting parenthesized lambda to simple lambda:\n{0}", apmInvocation);
                        rewrittenSyntax = RewriteParenthesizedLambdaToSimpleLambda(rewrittenSyntax, apmInvocation, apmSymbol);
                        break;

                    case SyntaxKind.ObjectCreationExpression:
                        Logger.Info("Rewriting object creation expression to simple lambda:\n{0}", apmInvocation);
                        var objectCreation = (ObjectCreationExpressionSyntax)callbackExpression;
                        rewrittenSyntax = RewriteObjectCreationToSimpleLambda(rewrittenSyntax, objectCreation);
                        break;

                    case SyntaxKind.AnonymousMethodExpression:
                        Logger.Info("Rewriting anonymous method (delegate) expression to simple lambda:\n{0}", apmInvocation);
                        var anonymousMethod = (AnonymousMethodExpressionSyntax)callbackExpression;
                        rewrittenSyntax = RewriteAnonymousMethodToSimpleLambda(rewrittenSyntax, anonymousMethod);
                        break;

                    case SyntaxKind.NullLiteralExpression:
                        message = String.Format("callback is null:\n{0}", apmInvocation.ContainingMethod());
                        Logger.Error("Precondition failed: {0}", message);
                        return document;

                    case SyntaxKind.InvocationExpression:
                        message = String.Format(
                                "InvocationExpression as callback is not supported: {0}",
                                apmInvocation
                            );
                        Logger.Error("Precondition failed: {0}", message);
                        return document;

                    case SyntaxKind.GenericName:
                        message = String.Format("GenericName syntax kind is not supported");
                        Logger.Error("Precondition failed: {0}", message);
                        return document;

                    default:
                        message = String.Format(
                            "Unsupported actual argument syntax node kind: {0}: callback argument: {1}: in method:\n{2}",
                            callbackExpression.Kind(),
                            callbackArgument,
                            apmInvocation.ContainingMethod()
                        );
                        Logger.Error(message);
                        return document;
                }
            }

            //if (rewrittenSolution.CompilationErrorCount() > numErrorsInSolutionBeforeRewriting)
            //{
            //    Logger.Error(
            //        "Rewritten solution contains more compilation errors than the original solution while refactoring: {0} @ {1}:{2} in method:\n{3}",
            //        beginXxxCall,
            //        beginXxxCall.SyntaxTree.FilePath,
            //        beginXxxCall.GetStartLineNumber(),
            //        beginXxxCall.ContainingMethod()
            //    );

            //    Logger.Warn("=== SOLUTION ERRORS ===");
            //    foreach (var diagnostic in rewrittenSolution.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            //    {
            //        Logger.Warn("  - {0}", diagnostic);
            //    }
            //    Logger.Warn("=== END OF SOLUTION ERRORS ===");

            //    Logger.Warn("\n### ORIGINAL CODE ###\n{0}### END OF CODE ###", syntax.Format(workspace));
            //    Logger.Warn("\n### REWRITTEN CODE ###\n{0}### END OF CODE ###", rewrittenSyntax.Format(workspace));

            //    throw new RefactoringException("Rewritten solution contains more compilation errors than the original refactoring");
            //}
        }

        public static CompilationUnitSyntax RewriteStateArgumentToNull(SimpleLambdaExpressionSyntax lambda, CompilationUnitSyntax syntax, ArgumentSyntax stateArgument)
        {
            if (lambda == null) throw new ArgumentNullException("lambda");
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (stateArgument == null) throw new ArgumentNullException("stateArgument");

            String message;

            var count = lambda.GetReferencesToParameterInBody().Count();

            if (count < 1)
            {
                message = String.Format(
                    "Lambda parameter '{0}' is never used",
                     lambda.Parameter.Identifier
                );

                Logger.Error(message);

                throw new PreconditionException(message);
            }

            if (count == 1) // Assumes IAsyncResult is only used in the EndXxx statement.
            {
                return syntax
                    .ReplaceNode(
                        stateArgument.Expression,
                        NewNullLiteral()
                    );
            }

            if (count == 2) // Assumes IAsyncResult is first used to retrieve AsyncState, then for EndXxx.
            {
                BlockSyntax block;
                switch (lambda.Body.Kind())
                {
                    case SyntaxKind.Block:
                        block = (BlockSyntax)lambda.Body;
                        break;

                    default:
                        throw new PreconditionException("Lambda body must be a block");
                }

                switch (stateArgument.Expression.Kind())
                {
                    case SyntaxKind.IdentifierName:
                        var statement = FindFirstStatementReferencingAsyncState(block);
                        var identifier = FindAsyncStateVariableName(statement);

                        var references = FindAllReferencesInBlock(block, identifier);
                        var stateId = (IdentifierNameSyntax)stateArgument.Expression;
                        var replacements = references.Select(reference => new SyntaxReplacementPair(reference, stateId));

                        var newBody = block.ReplaceAll(replacements);

                        // Remove first occurrence of AsyncState. Fingers crossed ...
                        newBody = newBody
                            .RemoveNode(
                                newBody
                                    .DescendantNodes()
                                    .OfType<LocalDeclarationStatementSyntax>()
                                    .First(node => node.ToString().Contains("AsyncState")),
                                SyntaxRemoveOptions.KeepNoTrivia
                            );

                        return syntax.ReplaceAll(
                            new SyntaxReplacementPair(block, newBody),
                            new SyntaxReplacementPair(stateArgument.Expression, NewNullLiteral())
                        );

                    default:
                        throw new NotImplementedException();
                }
            }

            // Rest of the method executes if count > 2 or if its not the regular pattern of AsyncState casting/EndXxx usage

            message = String.Format(
                "Lambda parameter '{0}' is used other than as EndXxx 'result' argument",
                lambda.Parameter.Identifier
            );

            Logger.Error(message);

            foreach (var reference in lambda.GetReferencesToParameterInBody())
            {
                Logger.Error(
                    "Reference: {0} @ {1}",
                    reference.ContainingStatement(),
                    reference.GetStartLineNumber()
                );
            }

            throw new PreconditionException(message);
        }

        private static IEnumerable<IdentifierNameSyntax> FindAllReferencesInBlock(BlockSyntax block, SyntaxToken identifier)
        {
            if (block == null) throw new ArgumentNullException("block");
            if (identifier == null) throw new ArgumentNullException("identifier");

            return block
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(node => node.Identifier.ValueText.Equals(identifier.ValueText));
        }

        private static IEnumerable<IdentifierNameSyntax> GetReferencesToParameterInBody(this SimpleLambdaExpressionSyntax lambda)
        {
            return lambda.Body.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(name => name.Identifier.ValueText.Equals(lambda.Parameter.Identifier.ValueText));
        }

        public static CompilationUnitSyntax RewriteInvocationExpressionToBlock(CompilationUnitSyntax syntax, SimpleLambdaExpressionSyntax lambda, SemanticModel model, InvocationExpressionSyntax beginXxxCall, IMethodSymbol apmSymbol)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (lambda == null) throw new ArgumentNullException("lambda");
            if (model == null) throw new ArgumentNullException("model");
            if (beginXxxCall == null) throw new ArgumentNullException("beginXxxCall");

            var callbackInvocation = (InvocationExpressionSyntax)lambda.Body;

            var stateArgument = FindAsyncStateInvocationArgument(beginXxxCall, apmSymbol);
            var stateExpression = stateArgument.Expression;

            var replacements = new List<SyntaxReplacementPair>();

            ArgumentListSyntax argumentList;
            if (stateExpression.Kind() == SyntaxKind.NullLiteralExpression)
            {
                argumentList = callbackInvocation.ArgumentList;
            }
            else
            {
                argumentList = callbackInvocation.ArgumentList.AddArguments(
                    SyntaxFactory.Argument(stateExpression)
                );

                var originalCallbackMethodSymbol = model.LookupMethodSymbol(callbackInvocation);
                var originalCallbackMethod = (MethodDeclarationSyntax)originalCallbackMethodSymbol.DeclaringSyntaxReferences.First().GetSyntax();
                var rewrittenCallbackMethod = RewriteCallbackWithIntroducedAsyncStateParameter(model, originalCallbackMethod, stateExpression);

                replacements.Add(new SyntaxReplacementPair(stateExpression, NewNullLiteral()));
                replacements.Add(new SyntaxReplacementPair(originalCallbackMethod, rewrittenCallbackMethod));
            }

            var newLambdaBody = NewBlock(
                SyntaxFactory.ExpressionStatement(callbackInvocation.WithArgumentList(argumentList))
            );

            replacements.Add(new SyntaxReplacementPair(lambda.Body, newLambdaBody));

            return syntax.ReplaceAll(replacements);
        }

        public static CompilationUnitSyntax RewriteMethodReferenceToSimpleLambda(CompilationUnitSyntax syntax, InvocationExpressionSyntax beginXxxCall, IMethodSymbol apmSymbol, SemanticModel model, ArgumentSyntax callbackArgument, ExpressionSyntax callbackExpression)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (beginXxxCall == null) throw new ArgumentNullException("beginXxxCall");
            if (model == null) throw new ArgumentNullException("model");

            var lambdaParamName = FindFreeIdentifier(beginXxxCall.ContainingMethod(), DefaultLambdaParamName);

            var stateArgument = FindAsyncStateInvocationArgument(beginXxxCall, apmSymbol);
            var stateExpression = stateArgument.Expression;

            var lambdaParamRef = SyntaxFactory.IdentifierName(lambdaParamName);

            IMethodSymbol originalCallbackMethodSymbol;
            try
            {
                switch (callbackExpression.Kind())
                {
                    case SyntaxKind.IdentifierName:
                        originalCallbackMethodSymbol = model.LookupMethodSymbol((IdentifierNameSyntax)callbackExpression);
                        break;

                    case SyntaxKind.SimpleMemberAccessExpression:
                        originalCallbackMethodSymbol = model.LookupMethodSymbol((MemberAccessExpressionSyntax)callbackExpression);
                        break;

                    default:
                        var message = String
                            .Format(
                                "Callback expression kind '{0}' not supported (this shouldn't happen), in:\n{1}",
                                callbackExpression.Kind(),
                                beginXxxCall.ContainingMethod()
                            );

                        throw new NotImplementedException(message);
                }
            }
            catch (MethodSymbolMissingException)
            {
                var message = String
                    .Format(
                        "Failed to look up method symbol for callback identifier: {0} in:\n{1}",
                        callbackExpression,
                        beginXxxCall.ContainingMethod()
                    );

                Logger.Warn(message);

                throw new PreconditionException(message, beginXxxCall);
            }

            var originalCallbackMethod = (MethodDeclarationSyntax)originalCallbackMethodSymbol.DeclaringSyntaxReferences.First().GetSyntax();

            ArgumentListSyntax argumentList;
            MethodDeclarationSyntax rewrittenCallbackMethod;
            if (stateExpression.Kind() == SyntaxKind.NullLiteralExpression)
            {
                // TODO: Replace with NewArgumentList (that is untested!!!)
                argumentList = NewSingletonArgumentList(
                    lambdaParamRef
                );
                rewrittenCallbackMethod = originalCallbackMethod;
            }
            else
            {
                argumentList = NewArgumentList(
                    lambdaParamRef,
                    stateExpression
                );

                rewrittenCallbackMethod = RewriteCallbackWithIntroducedAsyncStateParameter(model, originalCallbackMethod, stateExpression);
            }

            var lambda = SyntaxFactory.SimpleLambdaExpression(
                             NewUntypedParameter(lambdaParamName),
                             NewBlock(
                                NewInvocationStatement(
                                    callbackArgument.Expression,
                                    argumentList
                                )
                             )
                         );

            return syntax.ReplaceAll(
                new SyntaxReplacementPair(
                    callbackArgument.Expression,
                    lambda
                ),
                new SyntaxReplacementPair(
                    stateExpression,
                    NewNullLiteral()
                ),
                new SyntaxReplacementPair(
                    originalCallbackMethod,
                    rewrittenCallbackMethod
                )
            );
        }

        private static MethodDeclarationSyntax RewriteCallbackWithIntroducedAsyncStateParameter(SemanticModel model, MethodDeclarationSyntax callbackMethod, ExpressionSyntax stateExpression)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (callbackMethod == null) throw new ArgumentNullException("callbackMethod");
            if (stateExpression == null) throw new ArgumentNullException("stateExpression");

            var stateExpressionTypeSymbol = model.GetTypeInfo(stateExpression).Type;
            var newParameterTypeName = stateExpressionTypeSymbol.Name;

            var statement = FindFirstStatementReferencingAsyncState(callbackMethod.Body);
            var identifier = FindAsyncStateVariableName(statement);

            return callbackMethod
                .RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia)
                .AddParameterListParameters(
                    NewParameter(
                        SyntaxFactory.IdentifierName(newParameterTypeName),
                        identifier
                    )
                );
        }

        private static LocalDeclarationStatementSyntax FindFirstStatementReferencingAsyncState(BlockSyntax block)
        {
            if (block == null) throw new ArgumentNullException("block");

            return block
                .DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .First(stmt => stmt.ToString().Contains("AsyncState"));
        }

        public static CompilationUnitSyntax RewriteParenthesizedLambdaToSimpleLambda(CompilationUnitSyntax syntax, InvocationExpressionSyntax invocation, IMethodSymbol apmSymbol)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (invocation == null) throw new ArgumentNullException("invocation");

            var callbackArgument = FindAsyncCallbackInvocationArgument(invocation, apmSymbol);
            var parenthesizedLambda = (ParenthesizedLambdaExpressionSyntax)callbackArgument.Expression;

            var simpleLambda = SyntaxFactory.SimpleLambdaExpression(
                parenthesizedLambda.ParameterList.Parameters.First(),
                parenthesizedLambda.Body
            );

            return syntax.ReplaceNode(
                (SyntaxNode)parenthesizedLambda,
                simpleLambda
            );
        }

        public static CompilationUnitSyntax RewriteObjectCreationToSimpleLambda(CompilationUnitSyntax syntax, ObjectCreationExpressionSyntax objectCreation)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (objectCreation == null) throw new ArgumentNullException("objectCreation");

            if (!objectCreation.Type.ToString().Equals("AsyncCallback"))
            {
                Logger.Error("Unknown ObjectCreation type in callback: {0}", objectCreation);

                throw new NotImplementedException("Unknown ObjectCreation type in callback: " + objectCreation);
            }

            var expression = objectCreation.ArgumentList.Arguments.First().Expression;

            switch (expression.Kind())
            {
                case SyntaxKind.SimpleLambdaExpression:
                case SyntaxKind.ParenthesizedLambdaExpression:
                case SyntaxKind.IdentifierName:
                case SyntaxKind.ObjectCreationExpression:
                case SyntaxKind.AnonymousMethodExpression:
                    return syntax.ReplaceNode(
                        (SyntaxNode)objectCreation,
                        expression
                    );

                default:
                    Logger.Error("Unsupported expression type as argument of AsyncCallback constructor: {0}: {1}", expression.Kind(), objectCreation);

                    throw new NotImplementedException("Unsupported expression type as argument of AsyncCallback constructor: " + expression.Kind() + ": " + objectCreation);
            }
        }

        public static CompilationUnitSyntax RewriteAnonymousMethodToSimpleLambda(CompilationUnitSyntax syntax, AnonymousMethodExpressionSyntax anonymousMethod)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (anonymousMethod == null) throw new ArgumentNullException("anonymousMethod");

            if (anonymousMethod.ParameterList.Parameters.Count != 1)
                throw new ArgumentException("Anonymous method should have single parameter: " + anonymousMethod);

            var identifier = anonymousMethod.ParameterList.Parameters.First().Identifier;

            ExpressionSyntax lambda = SyntaxFactory.SimpleLambdaExpression(
                    SyntaxFactory.Parameter(identifier),
                    anonymousMethod.Block
                );

            return syntax
                .ReplaceNode(
                    anonymousMethod,
                    lambda
                );
        }

        public static CompilationUnitSyntax RefactorSimpleLambdaInstance(CompilationUnitSyntax syntax, InvocationExpressionSyntax beginXxxCall, IMethodSymbol apmSymbol, SemanticModel model, ArgumentSyntax callbackArgument)
        {
            var lambda = (SimpleLambdaExpressionSyntax)callbackArgument.Expression;

            if (lambda.Body.Kind() != SyntaxKind.Block)
                throw new NotImplementedException("Lambda body must be rewritten as BlockSyntax - it is now: " + lambda.Body.Kind() + ": lambda: " + lambda);

            var lambdaBlock = (BlockSyntax)lambda.Body;

            var stateArgument = FindAsyncStateInvocationArgument(beginXxxCall, apmSymbol);
            if (stateArgument.Expression.Kind() != SyntaxKind.NullLiteralExpression)
                throw new PreconditionException("APM Begin method invocation `state' argument must be null - it is now: " + stateArgument.Expression.Kind() + ": " + stateArgument);

            var originatingMethodSyntax = beginXxxCall.ContainingMethod();

            if (!originatingMethodSyntax.ReturnsVoid())
            {
                throw new PreconditionException("Initiating method does not return void");
            }

            // TODO: Look up the symbol to check that it actually exists.
            var methodNameBase = GetAsyncMethodNameBase(beginXxxCall);

            var endStatement = TryFindEndXxxCallSyntaxNode(lambdaBlock, methodNameBase);

            if (endStatement != null)
            {
                return RewriteNotNestedInstance(syntax, beginXxxCall, lambdaBlock, endStatement, methodNameBase);
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
                        beginXxxCall,
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

            var rewrittenSyntax = syntax
                .ReplaceAll(replacements)
                .WithUsingSystemThreadingTasks()
                .WithAdditionalAnnotations(Simplifier.Annotation);

            return rewrittenSyntax;
        }

        private static CompilationUnitSyntax RewriteNotNestedInstance(CompilationUnitSyntax syntax, InvocationExpressionSyntax beginXxxCall, BlockSyntax lambdaBlock, InvocationExpressionSyntax endStatement, string methodNameBase)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");
            if (beginXxxCall == null) throw new ArgumentNullException("beginXxxCall");
            if (lambdaBlock == null) throw new ArgumentNullException("lambdaBlock");
            if (endStatement == null) throw new ArgumentNullException("endStatement");
            if (methodNameBase == null) throw new ArgumentNullException("methodNameBase");

            var originalCallingMethod = beginXxxCall.ContainingMethod();

            var taskName = FreeTaskName(originalCallingMethod);

            var awaitStatement = NewAwaitExpression(taskName);
            var rewrittenLambdaBlock = lambdaBlock.ReplaceNode(endStatement, awaitStatement);

            var newCallingMethod = RewriteOriginatingMethod(beginXxxCall, rewrittenLambdaBlock, methodNameBase, taskName);

            var newSyntax = syntax
                .ReplaceNode(originalCallingMethod, newCallingMethod)
                .WithUsingSystemThreadingTasks()
                .WithAdditionalAnnotations(Simplifier.Annotation);

            return newSyntax;
        }

        private static CompilationUnitSyntax WithUsingSystemThreadingTasks(this CompilationUnitSyntax syntax)
        {
            if (syntax == null) throw new ArgumentNullException("syntax");

            if (syntax.Usings.Any(u => u.ToString().Equals("using System.Threading.Tasks;")))
                return syntax;

            var systemThreadingTasks = SyntaxFactory.UsingDirective(
                SyntaxFactory.QualifiedName(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Threading")
                        ),
                    SyntaxFactory.IdentifierName("Tasks")
                    )
                );

            return syntax.AddUsings(systemThreadingTasks);
        }

        private static MethodDeclarationSyntax RewriteOriginatingMethod(InvocationExpressionSyntax beginXxxCall, BlockSyntax rewrittenLambdaBlock, string methodNameBase, string taskName)
        {
            if (beginXxxCall == null) throw new ArgumentNullException("beginXxxCall");
            if (rewrittenLambdaBlock == null) throw new ArgumentNullException("rewrittenLambdaBlock");
            if (methodNameBase == null) throw new ArgumentNullException("methodNameBase");
            if (taskName == null) throw new ArgumentNullException("taskName");

            var tapStatement = NewTAPStatement(beginXxxCall, methodNameBase, taskName);
            var beginXxxStatement = beginXxxCall.ContainingStatement();

            var originalContainingBlock = beginXxxStatement.ContainingBlock();

            var rewrittenContainingBlock = originalContainingBlock
                .ReplaceNode(
                    beginXxxStatement,
                    tapStatement
                )
                .AddStatements(rewrittenLambdaBlock.Statements.ToArray());

            var originalInitiatingMethod = beginXxxCall.ContainingMethod();

            var rewrittenMethod = originalInitiatingMethod
                .ReplaceNode(
                    originalContainingBlock,
                    rewrittenContainingBlock
                );

            if (!rewrittenMethod.HasAsyncModifier())
                rewrittenMethod = rewrittenMethod.AddModifiers(NewAsyncKeyword());

            return rewrittenMethod;
        }

        private static StatementSyntax NewTAPStatement(InvocationExpressionSyntax beginXxxCall, string methodNameBase, string taskName)
        {
            // TODO: Introduce switch on beginXxxCall.Expression.Kind: beginXxxCall.Expression does not have to be a MemberAccessExpression.
            var coreExpression = ((MemberAccessExpressionSyntax)beginXxxCall.Expression).Expression;

            // NOTE: This naming method is only a heuristic, not a definition.
            var tapMethodName = methodNameBase + "Async";

            var expressions = beginXxxCall.ArgumentList.Arguments
                .Take(beginXxxCall.ArgumentList.Arguments.Count - 2)
                .Select(a => a.Expression);

            var expression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    coreExpression,
                    SyntaxFactory.IdentifierName(tapMethodName)
                ),
                NewArgumentList(expressions)
            );

            return NewVariableDeclarationStatement(taskName, expression);
        }

        /// <summary>
        /// Rewrite the originating method's lambda expression block so that its statements can be 'concatenated' to the originating method.
        /// </summary>
        /// <param name="lambda">The SimpleLambdaExpressionSyntax which must be rewritten.</param>
        /// <param name="callOnPathToEndXxxCall">The InvocationExpressionSyntax that represents the invocation of the callback in the lambda expression.</param>
        /// <param name="taskName">The name of the Task object that must be provided to the callback.</param>
        /// <returns>A rewritten BlockSyntax whose statements can be added to the originating method.</returns>
        private static BlockSyntax RewriteOriginatingMethodLambdaBlock(SimpleLambdaExpressionSyntax lambda, InvocationExpressionSyntax callOnPathToEndXxxCall, string taskName)
        {
            if (lambda == null) throw new ArgumentNullException("lambda");
            if (callOnPathToEndXxxCall == null) throw new ArgumentNullException("callOnPathToEndXxxCall");
            if (taskName == null) throw new ArgumentNullException("taskName");

            var asyncResultRefArg = callOnPathToEndXxxCall.ArgumentList.Arguments
                .First(arg => ((IdentifierNameSyntax)arg.Expression).Identifier.ValueText.Equals(lambda.Parameter.Identifier.ValueText));

            var awaitStatement = NewAwaitExpression(
                callOnPathToEndXxxCall.ReplaceNode(
                    asyncResultRefArg,
                    SyntaxFactory.Argument(
                        SyntaxFactory.IdentifierName(
                            taskName
                        )
                    )
                )
            );

            return ((BlockSyntax)lambda.Body).ReplaceNode(
                callOnPathToEndXxxCall,
                awaitStatement
            );
        }

        private static MethodDeclarationSyntax RewriteEndXxxContainingMethod(InvocationExpressionSyntax endXxxCall, string taskType)
        {
            const string taskName = DefaultTaskName;

            var originalMethod = endXxxCall.ContainingMethod();
            var returnType = NewTaskifiedReturnType(originalMethod);

            var replacements = new List<SyntaxReplacementPair>();

            var asyncResultParameter = FindIAsyncResultParameter(originalMethod.ParameterList);
            var taskParameter = NewGenericTaskParameter(taskName, taskType);
            replacements.Add(new SyntaxReplacementPair(asyncResultParameter, taskParameter));

            replacements.Add(new SyntaxReplacementPair(
                endXxxCall,
                NewAwaitExpression(taskName)
            ));

            var asyncResultParamName = asyncResultParameter.Identifier.ValueText;

            var newMethod = originalMethod.ReplaceAll(replacements);
            var newMethodBody = newMethod.Body;

            // TODO: Use find-all-references, or manual data flow analysis.
            var nodes = from node in newMethodBody.DescendantNodes()
                                                  .OfType<LocalDeclarationStatementSyntax>()
                        where NodeIsNotContainedInLambdaExpression(node, newMethodBody)
                        where node.DescendantNodes()
                                  .OfType<IdentifierNameSyntax>()
                                  .Any(id => id.Identifier.ValueText.Equals(asyncResultParamName))
                        select node;

            newMethod = newMethod.RemoveNodes(nodes, SyntaxRemoveOptions.KeepNoTrivia);

            return newMethod
                .AddModifiers(NewAsyncKeyword())
                .WithReturnType(returnType);
        }

        private static MethodDeclarationSyntax RewriteCallGraphPathComponent(InvocationExpressionSyntax invocation, String taskType)
        {
            if (invocation == null) throw new ArgumentNullException("invocation");

            const string taskName = DefaultTaskName;

            var method = invocation.ContainingMethod();

            var asyncResultParam = FindIAsyncResultParameter(method.ParameterList);

            var returnType = NewGenericTask(method.ReturnType);

            var taskParam = NewGenericTaskParameter(taskName, taskType);
            var parameterList = method.ParameterList.ReplaceNode(asyncResultParam, taskParam);

            var taskRef = SyntaxFactory.IdentifierName(taskName);

            var replacements = method.Body.DescendantNodes()
                                                        .OfType<IdentifierNameSyntax>()
                                                        .Where(id => id.Identifier.ValueText.Equals(asyncResultParam.Identifier.ValueText))
                                                        .Select(asyncResultRef => new SyntaxReplacementPair(asyncResultRef, taskRef))
                                                        .ToList();
            replacements.Add(AwaitedReplacementForCallGraphComponentInvocation(invocation, asyncResultParam, taskRef));

            var body = method.Body.ReplaceAll(replacements);

            return method.AddModifiers(NewAsyncKeyword())
                         .WithReturnType(returnType)
                         .WithParameterList(parameterList)
                         .WithBody(body);
        }

        private static SyntaxReplacementPair AwaitedReplacementForCallGraphComponentInvocation(InvocationExpressionSyntax invocation, ParameterSyntax asyncResultParam, IdentifierNameSyntax taskRef)
        {
            var invocationAsyncResultRef = invocation.DescendantNodes()
                                                     .OfType<IdentifierNameSyntax>()
                                                     .First(id => id.Identifier.ValueText.Equals(asyncResultParam.Identifier.ValueText));

            var awaitReplacement = new SyntaxReplacementPair(
                invocation,
                NewAwaitExpression(
                    invocation.ReplaceNode(invocationAsyncResultRef, taskRef)
                )
            );

            return awaitReplacement;
        }

        private static List<InvocationExpressionSyntax> TryFindCallGraphPathToEndXxx(BlockSyntax block, String methodNameBase, SemanticModel model)
        {
            var endXxxNode = TryFindEndXxxCallSyntaxNode(block, methodNameBase);

            if (endXxxNode != null)
            {
                return new List<InvocationExpressionSyntax> { endXxxNode };
            }

            var candidates = block.DescendantNodes()
                                  .OfType<InvocationExpressionSyntax>()
                                  .Where(node => NodeIsNotContainedInLambdaExpression(node, block));

            foreach (var candidate in candidates)
            {
                IMethodSymbol methodSymbol;
                try
                {
                    methodSymbol = model.LookupMethodSymbol(candidate);
                }
                catch (SymbolMissingException)
                {
                    Logger.Trace("Symbol missing for candidate: {0} - ignoring ...", candidate);
                    continue;
                }

                var methodSyntax = methodSymbol.FindMethodDeclarationNode();
                var potentialPath = TryFindCallGraphPathToEndXxx(methodSyntax.Body, methodNameBase, model);

                if (!potentialPath.Any()) continue;

                potentialPath.Add(candidate);
                return potentialPath;
            }

            return new List<InvocationExpressionSyntax>();
        }

        private static InvocationExpressionSyntax TryFindEndXxxCallSyntaxNode(BlockSyntax lambdaBlock, string methodNameBase)
        {
            if (lambdaBlock == null) throw new ArgumentNullException("lambdaBlock");
            if (methodNameBase == null) throw new ArgumentNullException("methodNameBase");

            // TODO: Check for correct signature, etc.
            // This can be done much smarter by e.g. using the BeginXxx method symbol, looking up the corresponding EndXxx symobl, and filtering on that.

            // TODO: Also considier IdentifierName EndXxx instances.
            var endXxxExpression = lambdaBlock.DescendantNodes()
                                              .OfType<MemberAccessExpressionSyntax>()
                                              .Where(node => NodeIsNotContainedInLambdaExpression(node, lambdaBlock))
                                              .FirstOrDefault(stmt => stmt.Name.ToString().Equals("End" + methodNameBase));

            return endXxxExpression == null ? null : (InvocationExpressionSyntax) endXxxExpression.Parent;
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
                if (node.Kind() == SyntaxKind.SimpleLambdaExpression ||
                    node.Kind() == SyntaxKind.ParenthesizedLambdaExpression)
                {
                    return false;
                }

                node = node.Parent;
            }

            return true;
        }

        private static string GetAsyncMethodNameBase(InvocationExpressionSyntax invocation)
        {
            if (invocation == null) throw new ArgumentNullException("invocation");

            var expression = (MemberAccessExpressionSyntax)invocation.Expression;

            var apmMethodName = expression.Name.ToString();
            var methodNameBase = apmMethodName.Substring(5);
            return methodNameBase;
        }

        public static ClassDeclarationSyntax ContainingClass(this SyntaxNode node)
        {
            if (node == null) throw new ArgumentNullException("node");

            var parent = node.Parent;

            while (parent != null)
            {
                if (parent.Kind() == SyntaxKind.ClassDeclaration)
                {
                    return (ClassDeclarationSyntax)parent;
                }

                parent = parent.Parent;
            }

            return null;
        }

        /// <summary>
        /// Returns the method containing this node.
        /// </summary>
        /// This node is supposedly contained in the scope of a certain method.
        /// The MethodDeclarationSyntax node of that method will be returned.
        ///
        /// TODO: This method does not consider e.g. lambda expressions.
        ///
        /// <param name="node">The syntax node</param>
        /// <returns>The MethodDeclarationSyntax node of the method that contains the given syntax node, or null if it is not contained in a method.</returns>
        public static MethodDeclarationSyntax ContainingMethod(this SyntaxNode node)
        {
            if (node == null) throw new ArgumentNullException("node");

            var parent = node.Parent;

            while (parent != null)
            {
                if (parent.Kind() == SyntaxKind.MethodDeclaration)
                {
                    return (MethodDeclarationSyntax)parent;
                }

                parent = parent.Parent;
            }

            return null;
        }

        /// <summary>
        /// Returns the StatementSyntax containing this node.
        /// </summary>
        /// This node is supposedly contained in a StatementSyntax node.
        /// That StatementSyntax (or subclass) instance is returned.
        ///
        /// TODO: This method does not consider e.g. lambda expressions.
        ///
        /// <param name="node">The SyntaxNode of which the parent must be looked up.</param>
        /// <returns>The containing StatementSyntax node, or null if it is not contained in a statement.</returns>
        public static StatementSyntax ContainingStatement(this SyntaxNode node)
        {
            if (node == null) throw new ArgumentNullException("node");

            var parent = node.Parent;

            while (parent != null)
            {
                var syntax = parent as StatementSyntax;
                if (syntax != null)
                {
                    return syntax;
                }

                parent = parent.Parent;
            }

            return null;
        }

        public static BlockSyntax ContainingBlock(this SyntaxNode node)
        {
            if (node == null) throw new ArgumentNullException("node");

            var parent = node.Parent;

            while (parent != null)
            {
                var syntax = parent as BlockSyntax;
                if (syntax != null)
                {
                    return syntax;
                }
                parent = parent.Parent;
            }

            return null;
        }

        public static ArgumentSyntax FindAsyncCallbackInvocationArgument(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol)
        {
            const string parameterTypeName = "System.AsyncCallback";

            var parameterIndex = FindMethodParameterIndex(methodSymbol, parameterTypeName);
            var callbackArgument = invocation.ArgumentList.Arguments.ElementAt(parameterIndex);

            return callbackArgument;
        }

        public static ArgumentSyntax FindAsyncStateInvocationArgument(InvocationExpressionSyntax invocation, IMethodSymbol apmSymbol)
        {
            var parameterIndex = FindMethodParameterIndex(apmSymbol, "object", "state");
            var callbackArgument = invocation.ArgumentList.Arguments.ElementAt(parameterIndex);

            return callbackArgument;
        }

        private static int FindMethodParameterIndex(IMethodSymbol symbol, string typeName)
        {
            if (symbol == null) throw new ArgumentNullException("symbol");
            if (typeName == null) throw new ArgumentNullException("typeName");

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
            if (symbol == null) throw new ArgumentNullException("symbol");
            if (typeName == null) throw new ArgumentNullException("typeName");
            if (identifierName == null) throw new ArgumentNullException("identifierName");

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

        private static ParameterSyntax FindIAsyncResultParameter(ParameterListSyntax parameterList)
        {
            return parameterList.Parameters
                                .First(param => param.Type.ToString().Equals("IAsyncResult"));
        }

        private static SyntaxToken FindAsyncStateVariableName(StatementSyntax statement)
        {
            if (statement == null) throw new ArgumentNullException("statement");

            switch (statement.Kind())
            {
                case SyntaxKind.LocalDeclarationStatement:
                    var declaration = ((LocalDeclarationStatementSyntax)statement).Declaration;

                    if (declaration.Variables.Count != 1)
                        throw new NotImplementedException("AsyncState referenced in LocalDeclarationStatement with multiple variables: " + statement);

                    return declaration.Variables.First().Identifier;

                default:
                    throw new NotImplementedException("First statement that uses AsyncState has unknown kind: " + statement.Kind() + ": statement: " + statement);
            }
        }

        private static StatementSyntax NewVariableDeclarationStatement(string resultName, ExpressionSyntax expression)
        {
            if (resultName == null) throw new ArgumentNullException("resultName");
            if (expression == null) throw new ArgumentNullException("expression");

            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    NewVarTypeSyntax(),
                    SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>(
                        new VariableDeclaratorSyntax[] {
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(resultName),
                                null,
                                SyntaxFactory.EqualsValueClause(expression))
                        }
                    )
                )
            );
        }

        private static ExpressionSyntax NewAwaitExpression(ExpressionSyntax expression)
        {
            if (expression == null) throw new ArgumentNullException("expression");

            var code = String.Format(@"await {0}.ConfigureAwait(false)", expression);

            return SyntaxFactory.ParseExpression(code);
        }

        private static ExpressionSyntax NewAwaitExpression(string taskName)
        {
            if (taskName == null) throw new ArgumentNullException("taskName");

            var code = String.Format(@"await {0}.ConfigureAwait(false)", taskName);

            return SyntaxFactory.ParseExpression(code);
        }

        private static SyntaxToken NewAsyncKeyword()
        {
            return SyntaxFactory.Token(
                SyntaxKind.AsyncKeyword
            );
        }

        private static TypeSyntax NewVarTypeSyntax()
        {
            return SyntaxFactory.IdentifierName("var");
        }

        private static LiteralExpressionSyntax NewNullLiteral()
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        }

        private static ParameterSyntax NewUntypedParameter(string name)
        {
            if (name == null) throw new ArgumentNullException("name");

            return SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(name)
            );
        }

        private static TypeSyntax NewTaskifiedReturnType(MethodDeclarationSyntax originalMethod)
        {
            if (originalMethod == null) throw new ArgumentNullException("originalMethod");

            return originalMethod.ReturnsVoid()
                ? SyntaxFactory.ParseTypeName("Task")
                : NewGenericTask(originalMethod.ReturnType);
        }

        private static bool ReturnsVoid(this MethodDeclarationSyntax method)
        {
            return method.ReturnType.ToString().Equals("void");
        }

        private static ParameterSyntax NewGenericTaskParameter(string taskName, string parameterType)
        {
            if (taskName == null) throw new ArgumentNullException("taskName");

            return NewParameter(
                NewGenericTask(parameterType),
                SyntaxFactory.Identifier(taskName)
            );
        }

        private static ParameterSyntax NewParameter(TypeSyntax type, SyntaxToken name)
        {
            return SyntaxFactory.Parameter(
                SyntaxFactory.List<AttributeListSyntax>(),
                SyntaxFactory.TokenList(),
                type,
                name,
                null
            );
        }

        private static TypeSyntax NewGenericTask(string parameterType)
        {
            if (parameterType == null) throw new ArgumentNullException("parameterType");

            return NewGenericTask(
                SyntaxFactory.ParseTypeName(parameterType)
            );
        }

        private static TypeSyntax NewGenericTask(TypeSyntax parameter)
        {
            if (parameter == null) throw new ArgumentNullException("parameter");

            var identifier = SyntaxFactory.Identifier("Task");

            return NewGenericName(identifier, parameter);
        }

        private static GenericNameSyntax NewGenericName(SyntaxToken identifier, TypeSyntax returnType)
        {
            if (returnType == null) throw new ArgumentNullException("returnType");

            return SyntaxFactory.GenericName(
                identifier,
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[] { returnType }
                    )
                )
            );
        }

        private static BlockSyntax NewBlock<T>(params T[] statements) where T : StatementSyntax
        {
            if (statements == null) throw new ArgumentNullException("statements");

            return SyntaxFactory.Block(
                SyntaxFactory.List(
                    statements
                )
            );
        }

        private static ArgumentListSyntax NewSingletonArgumentList(ExpressionSyntax expression)
        {
            if (expression == null) throw new ArgumentNullException("expression");

            return SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                    new ArgumentSyntax[] {
                        SyntaxFactory.Argument(expression)
                    }
                )
            );
        }

        private static ArgumentListSyntax NewArgumentList(params ExpressionSyntax[] expressions)
        {
            if (expressions == null) throw new ArgumentNullException("expressions");

            return NewArgumentList((IEnumerable<ExpressionSyntax>)expressions);
        }

        private static ArgumentListSyntax NewArgumentList(IEnumerable<ExpressionSyntax> expressions)
        {
            if (expressions == null) throw new ArgumentNullException("expressions");

            return SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                    expressions.Select(
                        SyntaxFactory.Argument
                    )
                )
            );
        }

        private static ExpressionStatementSyntax NewInvocationStatement(ExpressionSyntax expression, ArgumentListSyntax argumentList)
        {
            if (expression == null) throw new ArgumentNullException("expression");
            if (argumentList == null) throw new ArgumentNullException("argumentList");

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    expression,
                    argumentList
                )
            );
        }

        private static T RemoveFirst<T>(this IList<T> list)
        {
            if (list == null) throw new ArgumentNullException("list");

            var element = list.ElementAt(0);

            list.RemoveAt(0);

            return element;
        }

        private static T RemoveLast<T>(this IList<T> list)
        {
            if (list == null) throw new ArgumentNullException("list");

            var element = list.Last();

            list.RemoveAt(list.Count - 1);

            return element;
        }

        public static int GetNumRefactoringCandidatesInDocument(this Document document)
        {
            return document.GetSyntaxRootAsync().Result
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Count(node => node.HasAnnotations("RefactorableAPMInstance"));
        }

        public static InvocationExpressionSyntax GetAnnotatedInvocation(this CompilationUnitSyntax root)
        {
            var temp = root.GetAnnotatedNodes(apmAnnotation).FirstOrDefault(a => a.Kind() == SyntaxKind.InvocationExpression);
            return temp == null ? null : (InvocationExpressionSyntax) temp;
        }

        public static int GetStartLineNumber(this SyntaxNode node)
        {
            if (node == null) throw new ArgumentNullException("node");

            if (node.SyntaxTree == null) throw new ArgumentException("node.SyntaxTree is null");

            return node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line;
        }

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
    }

    public class PreconditionException : Exception
    {
        public PreconditionException(string message)
            : base("Precondition failed: " + message)
        {
        }

        public PreconditionException(string message, SyntaxNode node)
            : base("Precondition failed: " + message + ": " + node.SyntaxTree.FilePath + ": " + node.GetStartLineNumber())
        {
        }
    }

    public class RefactoringException : Exception
    {
        public RefactoringException(string message)
            : base(message)
        {
        }

        public RefactoringException(string message, SymbolMissingException innerException)
            : base(message, innerException)
        {
        }

        public RefactoringException(string message, SyntaxNode node)
            : base(message + ": " + node.SyntaxTree.FilePath + ":" + node.GetStartLineNumber())
        {
        }
    }
}
