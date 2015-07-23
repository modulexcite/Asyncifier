using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.IO;
using System.Linq;

namespace TestApps
{
    internal class Test2
    {
        private static MSBuildWorkspace workspace = MSBuildWorkspace.Create();

        public static void execute()
        {
            //const string candidatesDir = @"C:\Users\david\Downloads\C# Projects\CodeplexMostDownloaded1000Projects";
            const string candidatesDir = @"C:\Users\david\Downloads\C# Projects\Candidates";

            Console.WriteLine("Searching {0} and all subdirectories for solution files ...", candidatesDir);
            var solutionFileNames = Directory.GetFiles(candidatesDir, "*.sln", SearchOption.AllDirectories);

            Console.WriteLine("Searching for interesting projects ...");
            foreach (var solutionFileName in solutionFileNames)
            {
                PrintInterestingProjects(solutionFileName);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }

        private static void PrintInterestingProjects(string solutionFileName)
        {
            var solution = TryLoadSolution(solutionFileName);

            if (solution != null)
            {
                var projects = solution.Projects
                    .Where(project => project.IsInteresting());

                if (projects.Any())
                {
                    Console.WriteLine("- {0}", solutionFileName);
                    foreach (var project in projects)
                    {
                        Console.WriteLine("-- {0}", project.FilePath);
                    }
                }
            }
        }

        private static Solution TryLoadSolution(string filename)
        {
            try
            {
                return workspace.OpenSolutionAsync(filename).Result;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    internal static class Extensions
    {
        public static bool IsInteresting(this Project project)
        {
                //return project.IsCSProject() && project.IsWP8Project();
                return true;
        }
    }
}