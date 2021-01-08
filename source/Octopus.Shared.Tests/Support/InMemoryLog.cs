using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tests.Support
{
    public class InMemoryLog : SystemLog
    {
        readonly ILog log;
        readonly BlockingCollection<LogEvent> events = new BlockingCollection<LogEvent>(1000);

        public InMemoryLog() : this(null)
        {
        }

        public InMemoryLog(ILog log)
        {
            this.log = log ?? new TestConsoleLog();
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
            events.Add(logEvent);
            log.Write(logEvent.Category, logEvent.Error, logEvent.MessageText);
        }

        public IReadOnlyCollection<LogEvent> GetLogEvents() => events.ToArray();

        public string GetLog()
        {
            return events.Aggregate(new StringBuilder(), (sb, e) => sb.AppendLine($"{e.Category} {e.MessageText} {e.Error}"), sb => sb.ToString());
        }

        public void AssertContains(LogCategory category, string partialString)
        {
            var match = events.FirstOrDefault(e => e.Category == category && CultureInfo.CurrentCulture.CompareInfo.IndexOf(e.MessageText, partialString, CompareOptions.IgnoreCase) >= 0);
            if (match == null) throw new Exception($"The log does not contain any {category} entry containing the substring {partialString}.");
        }

        public void AssertContains(string partialString)
        {
            var match = events.FirstOrDefault(e => CultureInfo.CurrentCulture.CompareInfo.IndexOf(e.MessageText, partialString, CompareOptions.IgnoreCase) >= 0);
            if (match == null) throw new Exception($"The log does not contain any entry containing the substring {partialString}.");
        }

        public override void Flush()
        {
        }
    }
}