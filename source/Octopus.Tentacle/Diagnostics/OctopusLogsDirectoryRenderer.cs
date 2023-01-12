using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace Octopus.Tentacle.Diagnostics
{
    [LayoutRenderer("octopusLogsDirectory")]
    public class OctopusLogsDirectoryRenderer : LayoutRenderer
    {
        public static readonly string DefaultLogsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.Combine("Octopus", "Logs"));

        public static readonly HashSet<string> History = new HashSet<string>();

        static OctopusLogsDirectoryRenderer()
        {
            // Normally, we log to a special directory under C:\Octopus\Logs. However, this folder is configurable - the user may use
            // D:\MyOctopus\Logs instead. Since we don't know at startup, but we may still need to log some things, we'll log them to
            // the local application data folder by default.
            try
            {
                if (string.IsNullOrEmpty(DefaultLogsDirectory)) throw new ArgumentException("Value cannot be null or empty.", nameof(DefaultLogsDirectory));

                if (!Directory.Exists(DefaultLogsDirectory))
                    Directory.CreateDirectory(DefaultLogsDirectory);

                History.Add(DefaultLogsDirectory);
            }
            catch
            {
                // ignored
            }

            LogsDirectory = DefaultLogsDirectory;
        }

        public static string[] LogsDirectoryHistory => History.OrderBy(x => x).ToArray();
        public static string LogsDirectory { get; internal set; }

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogsDirectory);
        }
    }
}