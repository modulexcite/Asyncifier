using System;
using System.IO;
using System.Linq;
using NLog;
using Microsoft.CodeAnalysis;
using Utilities;

namespace Refactoring_BatchTool
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\Mono.Data.Sqlite\Mono.Data.Sqlite.sln";
        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\Weather\Weather.sln";
        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\topaz-fuel-card-windows-phone\Topaz Fuel Card.sln";

        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\WAZDash\WAZDash7.1.sln";
        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\awful2\wp\Awful\Awful.WP7.sln";
        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\awful2\wp\Awful\Awful.WP8.sln";
        //private const string SolutionFile = @"C:\Users\david\Projects\UIUC\Candidates\Automatic\8digits-WindowsPhone-SDK-Sample-App\EightDigitsTest.sln";

        private const string CandidatesDir = @"D:\CodeCorpus\Refactoring\";
        private const int BatchSize = 3000;

        private const string RefactoredAppsFile = @"C:\Users\semih\Desktop\Logs\RefactoredApps.log";
        private static readonly string[] RefactoredApps =
            File.Exists(RefactoredAppsFile)
                ? File.ReadAllLines(RefactoredAppsFile)
                : new string[] { };

        static void Main()
        {
            //Logger.Info("Hello, world!");
            //Logger.Info("Results file header:");
            SolutionRefactoring.LogResultsFileHeader();
            //Logger.Info("Symbols file header:");
            SolutionRefactoring.LogSymbolsFileHeader();
            //Logger.Info("Starting ...");

            var apps = Directory.GetDirectories(CandidatesDir).Take(BatchSize);

            Logger.Info("Starting run over max {0} apps...", BatchSize);


            foreach(var app in apps)
            {
                Logger.Info("Running over app: {0}", app.Split('\\').Last());

                var solutionFilePaths = from f in Directory.GetFiles(app, "*.sln", SearchOption.AllDirectories)
                                    let directoryName = Path.GetDirectoryName(f)
                                    where !directoryName.Contains(@"\tags") &&
                                          !directoryName.Contains(@"\branches")
                                    select f;
                foreach (var solutionFilePath in solutionFilePaths.Where(path => !RefactoredApps.Any(path.Equals)))
                {
                    TryRunOverSolutionFile(solutionFilePath);
                }
            
            }



            Console.WriteLine(@"Press any key to quit ...");
            Console.ReadKey();
        }

        private static void TryRunOverSolutionFile(string solutionFile)
        {
            Logger.Info("Running over solution file: {0}", solutionFile);

            SolutionRefactoring refactoring;
            try
            {
                refactoring = RunOverSolutionFile(solutionFile);
            }
            catch (Exception e)
            {
                Logger.Error("%%% CRITICAL ERROR %%%");
                Logger.Error("%%% Caught unexpected exception during work on solution file: {0}", solutionFile);
                Logger.Error("%%% Caught exception: {0}:\n{1}", e.Message, e);

                return;
            }

            Logger.Info("!!! REFACTORING RESULTS !!!");
            Logger.Info("!!! * Total number of candidates for refactoring: {0}", refactoring.NumCandidates);
            Logger.Info("!!! * Number of precondition failures           : {0}", refactoring.NumPreconditionFailures);
            Logger.Info("!!! * Number of valid candidates                : {0}", refactoring.NumValidCandidates);
            Logger.Info("!!! * Number of succesful refactorings          : {0}", refactoring.NumSuccesfulRefactorings);
            Logger.Info("!!! * Number of failed refactorings             : {0}", refactoring.NumRefactoringFailures);
            Logger.Info("!!!    - Compilation failures    : {0}", refactoring.NumCompilationErrors);
            Logger.Info("!!!    - RefactoringExceptions   : {0}", refactoring.NumRefactoringExceptions);
            Logger.Info("!!!    - NotImplementedExceptions: {0}", refactoring.NumNotImplementedExceptions);
            Logger.Info("!!!    - Other exceptions        : {0}", refactoring.NumOtherExceptions);
            Logger.Info("!!! END OF RESULTS !!!");

            try
            {
                File.AppendAllText(RefactoredAppsFile, solutionFile + Environment.NewLine);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to write solution file path to RefactoredAppsFile after completing refactoring run: {0}: {1}\n{2}",
                    solutionFile, e.Message, e);
            }
        }

        private static SolutionRefactoring RunOverSolutionFile(String solutionPath)
        {
            if (solutionPath == null) throw new ArgumentNullException("solutionPath");

            Logger.Trace("Loading solution file: {0}", solutionPath);

            SolutionRefactoring refactoring;
            using (var workspace = MSBuildWorkspace.Create())
            {
                var solution = workspace.TryLoadSolutionAsync(solutionPath).Result;
                
                if (solution == null)
                {
                    Logger.Error("Failed to load solution file: {0}", solutionPath);
                    throw new Exception("Failed to load solution file: " + solutionPath);
                }

                refactoring = new SolutionRefactoring(workspace);
                refactoring.Run();

                workspace.CloseSolution();
            }

            return refactoring;
        }
    }
}
