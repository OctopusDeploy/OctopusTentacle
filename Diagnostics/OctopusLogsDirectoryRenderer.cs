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
        static OctopusLogsDirectoryRenderer()
        {
            // Normally, we log to a special directory under C:\Octopus\Logs. However, this folder is configurable - the user may use
            // D:\MyOctopus\Logs instead. Since we don't know at startup, but we may still need to log some things, we'll log them to 
            // the local application data folder instead.

            var specialFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Octopus\\Logs");
            try
            {
                if (!Directory.Exists(specialFolder))
                {
                    Directory.CreateDirectory(specialFolder);
                }

                LogsDirectory = specialFolder;
            }
                // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
        }

        public static string LogsDirectory;

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogsDirectory);
        }
    }
}