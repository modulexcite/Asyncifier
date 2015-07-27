using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.IO;
using System.Xml;
namespace Test
{
    internal class Test1
    {
        public static void execute()
        {
            string dir = @"c:\users\semih\documents\visual studio 2013\Projects\App1\";

            //var app = new AsyncAnalysis(@"C:\Users\Semih\Desktop\facebook-windows-phone-sample-master","facebook");
            MSBuildWorkspace workspace = MSBuildWorkspace.Create();
            var solutionPaths = Directory.GetFiles(dir, "*.sln", SearchOption.AllDirectories);
            foreach (var solutionPath in solutionPaths)
            {
                var solution = workspace.OpenSolutionAsync(solutionPath).Result;
                foreach (var project in solution.Projects)
                {
                    foreach (var refe in project.MetadataReferences)
                        Console.WriteLine(refe.Display);
                    Console.WriteLine("-----");
                }
            }
            Console.ReadLine();
        }

        public static int IsWindowsPhoneProject(Project project)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(project.FilePath);

            XmlNamespaceManager mgr = new XmlNamespaceManager(doc.NameTable);
            mgr.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

            var node = doc.SelectSingleNode("//x:TargetFrameworkIdentifier", mgr);
            if (node != null)
            {
                if (node.InnerText.ToString().Equals("WindowsPhone"))
                    return 2;
                else if (node.InnerText.ToString().Equals("Silverlight"))
                {
                    var profileNode = doc.SelectSingleNode("//x:TargetFrameworkProfile", mgr);
                    if (profileNode != null && profileNode.InnerText.ToString().Contains("WindowsPhone"))
                        return 1;
                }
            }
            return 0;
        }
    }
}