using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Tentacle.Tests.Support
{
    public class InMemoryLog : AbstractLog
    {
        readonly ILog log;
        readonly StringBuilder logText = new StringBuilder();

        public InMemoryLog() : this(null)
        {
        }

        public InMemoryLog(ILog log)
        {
            this.log = log ?? Log.Octopus();
        }

        public override LogContext CurrentContext => new LogContext();

        protected override void WriteEvent(LogEvent logEvent)
        {
            logText.AppendLine(logEvent.Category + " " + logEvent.MessageText + " " + logEvent.Error);
            log.Write(logEvent.Category, logEvent.Error, logEvent.MessageText);
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
            throw new NotImplementedException();
        }

        public override IDisposable WithinBlock(LogContext logContext)
        {
            return null;
        }

        public override void Flush()
        {
        }

        public override bool IsEnabled(LogCategory category)
        {
            return true;
        }

        public string GetLog()
        {
            return logText.ToString();
        }

        public void AssertContains(LogCategory category, string partialString)
        {
            var match = GetLog()
                .Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith(category.ToString()) &&
                    CultureInfo.CurrentCulture.CompareInfo.IndexOf(line, partialString, CompareOptions.IgnoreCase) >= 0);

            if (match == null) throw new Exception($"The log does not contain any {category} entry containing the substring {partialString}.");
        }

        public void AssertContains(string partialString)
        {
            var match = GetLog()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => CultureInfo.CurrentCulture.CompareInfo.IndexOf(line, partialString, CompareOptions.IgnoreCase) >= 0);

            if (match == null) throw new Exception($"The log does not contain any entry containing the substring {partialString}.");
        }
    }
}