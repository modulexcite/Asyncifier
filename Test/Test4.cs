using Analysis;
using System;

namespace Test
{
    internal class Test4
    {
        public static void execute()
        {
            var app = new AsyncAnalysis(@"Z:\C#PROJECTS\PhoneApps\4square", "test");

            app.Analyze();
            Console.ReadLine();
        }
    }
}