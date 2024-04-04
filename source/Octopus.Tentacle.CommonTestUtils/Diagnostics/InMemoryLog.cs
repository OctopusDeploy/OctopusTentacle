using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using FluentAssertions;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.CommonTestUtils.Diagnostics
{
    public class InMemoryLog : SystemLog
    {
        readonly ILog log;
        readonly BlockingCollection<LogEvent> events = new BlockingCollection<LogEvent>(1000);

        public InMemoryLog() : this(null)
        {
        }

        public InMemoryLog(ILog? log)
        {
            this.log = log ?? new TestConsoleLog();
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
            events.Add(logEvent);
            log.Write(logEvent.Category, logEvent.Error!, logEvent.MessageText);
        }

        public string GetLog()
        {
            return events.Aggregate(new StringBuilder(), (sb, e) => sb.AppendLine($"{e.Category} {e.MessageText} {e.Error}"), sb => sb.ToString());
        }

        public void AssertContains(string partialString)
        {
            events.Should().Contain(e => CultureInfo.CurrentCulture.CompareInfo.IndexOf(e.MessageText, partialString, CompareOptions.IgnoreCase) >= 0);
        }

        public override void Flush()
        {
        }
    }
}