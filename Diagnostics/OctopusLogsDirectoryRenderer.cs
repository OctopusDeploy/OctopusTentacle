using System;
using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace Octopus.Shared.Diagnostics
{
    [LayoutRenderer("octopusLogsDirectory")]
    public class OctopusLogsDirectoryRenderer : LayoutRenderer
    {
        public static string LogsDirectory = null;

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogsDirectory);
        }
    }
}