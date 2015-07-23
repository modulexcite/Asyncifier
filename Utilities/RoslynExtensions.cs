using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Xml;
using NLog;

namespace Utilities
{
    /// <summary>
    /// Several handy extension methods for Roslyn types.
    /// </summary>
    public static class RoslynExtensions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        public static int CountSLOC(this SyntaxNode node)
        {
            var text = node.GetText();
            var totalLines = text.Lines.Count();

            var linesWithNoText = 0;
            foreach (var l in text.Lines)
            {
                if (String.IsNullOrEmpty(l.ToString().Trim()))
                {
                    ++linesWithNoText;
                }
            }
            return totalLines - linesWithNoText; ;
        }

        public static string ToStringWithReturnType(this IMethodSymbol symbol)
        {
            var methodCallString = symbol.ToString();
            if (symbol.ReturnsVoid)
                methodCallString = "void " + methodCallString;
            else
                methodCallString = symbol.ReturnType.ToString() + " " + methodCallString;
            return methodCallString;
        }



        // (1) MAIN PATTERNS: TAP, EAP, APM
        public static bool IsTAPMethod(this IMethodSymbol symbol)
        {
            return (symbol.ReturnTask() && symbol.DeclaringSyntaxReferences.Count() == 0 && !symbol.ToString().StartsWith("System.Threading.Tasks") && !symbol.IsTaskCreationMethod())
                || (symbol.ReturnTask() && symbol.ToString().Contains("FromAsync"));
                
        }

        public static bool IsEAPMethod(this InvocationExpressionSyntax invocation)
        {
            return invocation.Expression.ToString().ToLower().EndsWith("async") &&
                   invocation.Ancestors().OfType<MethodDeclarationSyntax>().Any(node =>
                                                                           node.DescendantNodes()
                                                                           .OfType<BinaryExpressionSyntax>()
                                                                           .Any(a => a.Left.ToString().ToLower().EndsWith("completed")));
        }

        public static bool IsAPMBeginMethod(this IMethodSymbol symbol)
        {
            return !IsAsyncDelegate(symbol) && symbol.Parameters.ToString().Contains("AsyncCallback") && !(symbol.ReturnsVoid) && symbol.ReturnType.ToString().Contains("IAsyncResult");
        }

        // (2) WAYS OF OFFLOADING THE WORK TO ANOTHER THREAD: TPL, THREADING, THREADPOOL, ACTION/FUNC.BEGININVOKE,  BACKGROUNDWORKER
        public static bool IsTaskCreationMethod(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("System.Threading.Tasks.Task.Start")
                || symbol.ToString().Contains("System.Threading.Tasks.Task.Run")
                || symbol.ToString().Contains("System.Threading.Tasks.TaskFactory.StartNew")
                || symbol.ToString().Contains("System.Threading.Tasks.TaskEx.RunEx")
                || symbol.ToString().Contains("System.Threading.Tasks.TaskEx.Run")
                || symbol.ToString().Contains("StartNewTask")
                || symbol.ToString().Contains("StartNewTaskWithoutExceptionHandling");

        }

        public static bool IsThreadPoolQueueUserWorkItem(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("ThreadPool.QueueUserWorkItem");
        }

        public static bool IsBackgroundWorkerMethod(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("BackgroundWorker.RunWorkerAsync");
        }

        public static bool IsThreadStart(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Thread.Start");
        }

        public static bool IsParallelFor(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Parallel.For") ||
                   symbol.ToString().Contains("ParallelWithMember.For");
        }
        public static bool IsParallelForEach(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Parallel.ForEach") ||
                   symbol.ToString().Contains("ParallelWithMember.ForEach");
        }

        public static bool IsParallelInvoke(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Parallel.Invoke") ||
                   symbol.ToString().Contains("ParallelLoader.Invoke");
        }

        public static bool IsAsyncDelegate(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Invoke") &&
                !(symbol.ReturnsVoid) && symbol.ReturnType.ToString().Contains("IAsyncResult");
        }

        // (3) WAYS OF UPDATING GUI: CONTROL.BEGININVOKE, DISPATCHER.BEGININVOKE, ISYNCHRONIZE.BEGININVOKE

        public static bool IsISynchronizeInvokeMethod(this IMethodSymbol symbol)
        {
            return symbol.ToString().StartsWith("System.ComponentModel.ISynchronizeInvoke");
        }

        public static bool IsControlBeginInvoke(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Control.BeginInvoke");
        }

        public static bool IsDispatcherBeginInvoke(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Dispatcher.BeginInvoke");
        }

        public static bool IsDispatcherInvoke(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("Dispatcher.Invoke");
        }

        // END

        public static bool IsAPMEndMethod(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("IAsyncResult") && symbol.Name.StartsWith("End");
        }

        public static bool ReturnTask(this IMethodSymbol symbol)
        {
            return !symbol.ReturnsVoid && symbol.ReturnType.ToString().StartsWith("System.Threading.Tasks.Task");
        }

        public static bool IsInSystemWindows(this UsingDirectiveSyntax node)
        {
            return node.Name.ToString().StartsWith("System.Windows");
        }

        public static bool HasEventArgsParameter(this MethodDeclarationSyntax method)
        {
            return method.ParameterList.Parameters.Any(param => param.Type.ToString().EndsWith("EventArgs"));
        }


        public static bool HasAsyncModifier(this MethodDeclarationSyntax method)
        {
            return method.Modifiers.ToString().Contains("async");
        }

        public static MethodDeclarationSyntax FindMethodDeclarationNode(this IMethodSymbol methodCallSymbol)
        {
            if (methodCallSymbol == null)
                return null;

            var nodes = methodCallSymbol.DeclaringSyntaxReferences.Select(a => a.GetSyntax());

            if (nodes == null || nodes.Count() == 0)
                return null;

            var methodDeclarationNodes = nodes.OfType<MethodDeclarationSyntax>();

            if (methodDeclarationNodes.Count() != 0)
                return methodDeclarationNodes.First();

            return null;

            // above one is not always working. basically, above one is the shortcut for the below one!

            //var def = methodCallSymbol.FindSourceDefinition(currentSolution);

            //if (def != null && def.Locations != null && def.Locations.Count > 0)
            //{
            //    //methodCallSymbol.DeclaringSyntaxNodes.Firs
            //    var loc = def.Locations.First();

            //    Solution s;
            //    s.
            //    var node = loc.SourceTree.GetRoot().FindToken(loc.SourceSpan.Start).Parent;
            //    if (node is MethodDeclarationSyntax)
            //        return (MethodDeclarationSyntax)node;
            //}
        }

        public static string DetectSynchronousUsages(this IMethodSymbol methodCallSymbol, SemanticModel semanticModel)
        {
            var list = semanticModel.LookupSymbols(0, container: methodCallSymbol.ContainingType,
                                includeReducedExtensionMethods: true);

            var name = methodCallSymbol.Name;

            if (name.Equals("Sleep"))
            {
                return "Task.Delay";
            }

            foreach (var tmp in list)
            {
                //if (tmp.Name.Equals("Begin" + name))
                //{
                //    return tmp.Name;
                //}
                if (tmp.Name.Equals(name + "Async"))
                {
                    if(!name.Equals("Invoke"))
                        return tmp.Name;
                }
            }
            return "None";
        }



        public static int CompilationErrorCount(this Solution solution)
        {
            return solution
                .GetDiagnostics()
                .Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        public static IEnumerable<Diagnostic> GetDiagnostics(this Solution solution)
        {
            if (solution == null) throw new ArgumentNullException("solution");

            return solution.Projects
                .Select(project => project.GetCompilationAsync().Result)
                .SelectMany(compilation => compilation.GetDiagnostics());
        }

        public static async Task<Solution> TryLoadSolutionAsync(this MSBuildWorkspace workspace, string solutionPath)
        {
            if (workspace == null) throw new ArgumentNullException("workspace");
            if (solutionPath == null) throw new ArgumentNullException("solutionPath");

            Logger.Trace("Trying to load solution file: {0}", solutionPath);

            try
            {
                return await workspace.OpenSolutionAsync(solutionPath);
            }
            catch (Exception ex)
            {
                Logger.Warn("Solution not analyzed: {0}: Reason: {1}", solutionPath, ex.Message);

                return null;
            }
        }

    }
}
