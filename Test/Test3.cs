using System;
using System.Diagnostics;
using System.IO;

namespace Test
{
    internal class Test3
    {
        public static void execute()
        {
            var path = @"Z:\C#PROJECTS\PhoneApps\APMPhoneAppsBACKUP\beamartian\NASA.BeAMartian.sln";

            var command = @"devenv " + path + @" /upgrade";
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/C " + command);
            info.WindowStyle = ProcessWindowStyle.Hidden;

            Process p = Process.Start(info);
            p.WaitForExit();
            string dir = Path.GetDirectoryName(path) + @"\Backup\";
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Console.ReadLine();
        }
    }
}