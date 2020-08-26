using System;
using System.IO;

namespace Octopus.Upgrader
{
    public class Log
    {
        public static readonly Log Upgrade = new Log("UpgradeLog");
        public static readonly Log ExitCode = new Log("ExitCode");

        readonly string logFile;

        Log(string prefix)
        {
            logFile = Path.Combine(Environment.CurrentDirectory, prefix + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString().Substring(0, 5) + ".log");
        }

        public void Info(string message)
        {
            Console.WriteLine(message);
            using (var file = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(file))
            {
                writer.WriteLine(message);
                writer.Flush();
            }
        }
    }
}