using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilities;
using RoslynUtilities;

namespace Refactoring_BatchTool
{
    public class SolutionRefactoring
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Logger Results = LogManager.GetLogger("RESULTS");
        private static readonly Logger Symbols = LogManager.GetLogger("SYMBOLS");

        private static readonly Logger TempLog = LogManager.GetLogger("TempLog");

        private readonly Workspace _workspace;

        private readonly Solution _originalSolution;
        private Solution _refactoredSolution;

        private readonly List<APMRefactoring> _refactorings = new List<APMRefactoring>();

        public IEnumerable<APMRefactoring> Refactorings { get { return _refactorings; } }

        public int NumCandidates { get; private set; }
        public int NumPreconditionFailures { get; private set; }
        public int NumValidCandidates { get { return NumCandidates - NumPreconditionFailures; } }

        public int NumCompilationErrors { get; private set; }
        public int NumRefactoringExceptions { get; private set; }
        public int NumNotImplementedExceptions { get; private set; }
        public int NumOtherExceptions { get; private set; }

        public int NumRefactoringFailures { get { return NumCompilationErrors + NumRefactoringExceptions + NumNotImplementedExceptions + NumOtherExceptions; } }

        public int NumSuccesfulRefactorings
        {
            get { return NumValidCandidates - NumRefactoringFailures; }
        }

        public int NumMethodSymbolLookups { get; private set; }

        public SolutionRefactoring(Workspace workspace)
        {
            if (workspace == null) throw new ArgumentNullException("workspace");

            _workspace = workspace;

            _originalSolution = _workspace.CurrentSolution;
            Solution newSolution = _originalSolution;
            var projectlist = _originalSolution.Projects.AsEnumerable();
            
            foreach(var project in projectlist)
            {
                List<MetadataReference> list = new List<MetadataReference>();
                //foreach (var refer in project.MetadataReferences)
                //    // check whether they already reference microsoft.bcl.async
                //    list.Add(refer);

                if (project.MetadataReferences.Where(a => a.Display.ToString().Contains("AsyncCtpLibrary") || a.Display.ToString().Contains("Microsoft.Bcl.Async")).Any())
                    continue;

                if (project.IsWindowsPhoneProject() == 1)
                {
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.Async.1.0.14-rc\lib\sl4-windowsphone71\Microsoft.Threading.Tasks.dll"));
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.Async.1.0.14-rc\lib\sl4-windowsphone71\Microsoft.Threading.Tasks.Extensions.dll"));
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.Async.1.0.14-rc\lib\sl4-windowsphone71\Microsoft.Threading.Tasks.Extensions.Phone.dll"));
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.1.0.16-rc\lib\sl4-windowsphone71\System.Runtime.dll"));
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.1.0.16-rc\lib\sl4-windowsphone71\System.Threading.Tasks.dll"));
                }
                else if (project.IsWindowsPhoneProject() == 2)
                {
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.Async.1.0.14-rc\lib\wp8\Microsoft.Threading.Tasks.dll"));
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.Async.1.0.14-rc\lib\wp8\Microsoft.Threading.Tasks.Extensions.dll"));
                    list.Add(new MetadataFileReference(@"C:\Users\Semih\Desktop\lib\Microsoft.Bcl.Async.1.0.14-rc\lib\wp8\Microsoft.Threading.Tasks.Extensions.Phone.dll"));
                }
                else
                    continue;

                newSolution = newSolution.AddMetadataReferences(project.Id, list);


            }

            if (newSolution.CompilationErrorCount() > _originalSolution.CompilationErrorCount())
            {
                TempLog.Info("{0}", newSolution.FilePath);
                foreach (var diagnostic in newSolution.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    TempLog.Info("=== Solution error: {0}", diagnostic);
                }
                
            }

           _originalSolution = newSolution;
           
        }

        public void Run()
        {
            var documents = _originalSolution.Projects
                .Where(project => project.IsWindowsPhoneProject() > 0)
                .SelectMany(project => project.Documents)
                .Where(document => document.FilePath.EndsWith(".cs")) // Only cs files.
                /*.Where(document => !document.FilePath.EndsWith("Test.cs"))*/; // No tests.

            _refactoredSolution = documents.Aggregate(_originalSolution,
                (sln, doc) => CheckDocument(doc, sln));

            //Logger.Info("Applying changes to workspace ...");
            //if (!_workspace.TryApplyChanges(_refactoredSolution))
            //{
            //    const string message = "Failed to apply changes in solution to workspace";

            //    Logger.Error(message);

            //    throw new Exception(message);
            //}

            PrintResults();
        }

        private Solution CheckDocument(Document document, Solution solution)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (solution == null) throw new ArgumentNullException("solution");

            //Logger.Trace("Checking document: {0}", document.FilePath);

            var annotatedSolution = AnnotateDocumentInSolution(document, solution);

            return RefactorDocumentInSolution(document, annotatedSolution);
        }

        private Solution AnnotateDocumentInSolution(Document document, Solution solution)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (solution == null) throw new ArgumentNullException("solution");

            var annotater = new APMBeginInvocationAnnotater();
            var annotatedDocument = annotater.Annotate(document);

            NumCandidates += annotater.NumAnnotations;

            return solution.WithDocumentSyntaxRoot(
                document.Id,
                annotatedDocument.GetSyntaxRootAsync().Result
            );
        }

        private Solution RefactorDocumentInSolution(Document document, Solution solution)
        {
            var refactoredSolution = solution;
            var refactoredDocument = refactoredSolution.GetDocument(document.Id);
            var candidatesInDocument = refactoredDocument.GetNumRefactoringCandidatesInDocument();

            //Logger.Trace("Found {0} APM instances. Refactoring one-by-one ...", candidatesInDocument);
            for (var index = 0; index < candidatesInDocument; index++)
            {
                var beginXxxSyntax = refactoredDocument.GetAnnotatedInvocation(index);

                var containingStatement = beginXxxSyntax.ContainingStatement();
                switch (containingStatement.Kind)
                {
                    case SyntaxKind.ExpressionStatement:
                    case SyntaxKind.LocalDeclarationStatement:
                        var refactoring = new APMRefactoring(refactoredSolution, _workspace);

                        refactoredSolution = refactoring.SafelyRefactorSolution(refactoredSolution, refactoredDocument, index);
                        refactoredDocument = refactoredSolution.GetDocument(document.Id);

                        _refactorings.Add(refactoring);

                        if (refactoring.PreconditionFailed)
                        {
                            NumPreconditionFailures++;
                        }
                        else if (refactoring.NotImplementedExceptionWasThrown)
                        {
                            NumNotImplementedExceptions++;
                        }
                        else if (refactoring.CompilationError)
                        {
                            NumCompilationErrors++;
                        }
                        else if (refactoring.RefactoringExceptionWasThrown)
                        {
                            NumRefactoringExceptions++;
                        }
                        else if (refactoring.OtherExceptionWasThrown)
                        {
                            NumOtherExceptions++;
                        }
                        else if (refactoring.Succesful)
                        {
                            // OK.

                            var numMethodSymbolLookups = refactoring.NumMethodSymbolLookups;

                            LogSymbolLookupResults(solution, document, beginXxxSyntax, numMethodSymbolLookups);

                            NumMethodSymbolLookups += numMethodSymbolLookups;
                        }
                        else
                        {
                            throw new Exception("Unrecognized refactoring result");
                        }

                        break;

                    case SyntaxKind.ReturnStatement:
                        Logger.Info("Precondition failed: APM Begin method invocation contained in return statement");
                        NumPreconditionFailures++;
                        break;

                    default:
                        Logger.Warn(
                            "APM Begin invocation containing statement is not yet supported: index={0}: {1}: statement: {2}",
                            index,
                            beginXxxSyntax.ContainingStatement().Kind,
                            beginXxxSyntax.ContainingStatement()
                        );

                        NumNotImplementedExceptions++;
                        break;
                }
            }

            return refactoredSolution;
        }
        public static void LogSymbolsFileHeader()
        {
            Symbols.Info("solution,document,instanceLineNo,numMethodSymbolLookups");
        }

        private static void LogSymbolLookupResults(Solution solution, Document document, InvocationExpressionSyntax beginXxxSyntax, int numMethodSymbolLookups)
        {
            Symbols.Info(
                "{0},{1},{2},{3}",
                solution.FilePath,
                document.FilePath,
                beginXxxSyntax.GetStartLineNumber(),
                numMethodSymbolLookups
            );
        }
        public static void LogResultsFileHeader()
        {
            Results.Info("solution,numInstances,numPreconditionExceptions,numValidInstances,NumCompilationFailures,NumRefactoringExceptions,NumNotImplementedExceptions,NumOtherExceptions");
        }

        private void PrintResults()
        {
            Results.Info(
                "{0},{1},{2},{3},{4},{5},{6},{7}",
                _originalSolution.FilePath,
                NumCandidates,
                NumPreconditionFailures,
                NumValidCandidates,
                NumCompilationErrors,
                NumRefactoringExceptions,
                NumNotImplementedExceptions,
                NumOtherExceptions
            );
        }
    }
}
