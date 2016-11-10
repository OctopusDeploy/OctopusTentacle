using System;
using System.IO;
using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace Octopus.Shared.Diagnostics
{
    [LayoutRenderer("octopusLogsDirectory")]
    public class OctopusLogsDirectoryRenderer : LayoutRenderer
    {
        public static readonly string DefaultLogsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Octopus\\Logs");

        static OctopusLogsDirectoryRenderer()
        {
            // Normally, we log to a special directory under C:\Octopus\Logs. However, this folder is configurable - the user may use
            // D:\MyOctopus\Logs instead. Since we don't know at startup, but we may still need to log some things, we'll log them to 
            // the local application data folder instead.
            try
            {
                var logsDirectory = DefaultLogsDirectory;
                SetLogsDirectory(logsDirectory);
            }
            catch
            {
                // ignored
            }
        }

        public static void SetLogsDirectory(string logsDirectory)
        {
            if (string.IsNullOrEmpty(logsDirectory)) throw new ArgumentException("Value cannot be null or empty.", nameof(logsDirectory));

            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            LogsDirectory = logsDirectory;
        }

        public static string LogsDirectory { get; private set; }

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogsDirectory);
        }
    }
}