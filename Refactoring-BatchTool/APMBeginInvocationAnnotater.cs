using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NLog;
using Utilities;

namespace Refactoring_BatchTool
{
    public class APMBeginInvocationAnnotater : CSharpSyntaxRewriter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private SemanticModel _model;
        public int NumAnnotations { get; private set; }

        public APMBeginInvocationAnnotater()
        {
            NumAnnotations = 0;
        }

        public Document Annotate(Document document)
        {
            if (document == null) throw new ArgumentNullException("document");

            _model = (SemanticModel)document.GetSemanticModelAsync().Result;

            var originalTree = (SyntaxTree)document.GetSyntaxTreeAsync().Result;

            var annotatedTree = Visit(originalTree.GetRoot());

            return document.WithSyntaxRoot(annotatedTree);
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            //Logger.Trace("Visiting invocation: {0}", node);

            if (node == null) throw new ArgumentNullException("node");

            if (!node.ToString().Contains("Begin"))
                return node;

            //Logger.Debug("Found potential Begin method: {0} at {1}:{2}",
            //    node,
            //    node.SyntaxTree.FilePath,
            //    node.GetStartLineNumber()
            //);

            IMethodSymbol method;
            try
            {
                method = _model.LookupMethodSymbol(node);
            }
            catch (SymbolMissingException e)
            {
                //Logger.Trace("Symbol missing for invocation: {0} @ {1}:{2}: {3}",
                //    node,
                //    node.SyntaxTree.FilePath,
                //    node.GetStartLineNumber(),
                //    e.Message
                //);

                return node;
            }

            if (method.IsAPMBeginMethod())
            {
                //Logger.Info("Found APM Begin method invocation, annotating it: @ {0}:{1}",
                //    node.SyntaxTree.FilePath,
                //    node.GetStartLineNumber()
                //);
                //Logger.Trace("Actual code @ {0}:{1}:\n{2}",
                //    node.SyntaxTree.FilePath,
                //    node.GetStartLineNumber(),
                //    node
                //);

                var annotation = new RefactorableAPMInstance(NumAnnotations++);

                return base
                    .VisitInvocationExpression(node)
                    .WithAdditionalAnnotations(
                        annotation
                    );
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
