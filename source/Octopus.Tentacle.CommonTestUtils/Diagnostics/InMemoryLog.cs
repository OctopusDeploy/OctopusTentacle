using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.CommonTestUtils.Diagnostics
{
    public class InMemoryLog : SystemLog, ITentacleClientTaskLog
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

        public IReadOnlyList<string?> GetLogsForCategory(LogCategory category)
        {
            return events.Where(e => e.Category == category).Select(e => e.MessageText.ToString()).ToArray();
        }

        public void AssertContains(string partialString)
        {
            events.Should().Contain(e => CultureInfo.CurrentCulture.CompareInfo.IndexOf(e.MessageText, partialString, CompareOptions.IgnoreCase) >= 0);
        }

        public void AssertEventuallyContains(string partialString, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    AssertContains(partialString);
                    return;
                }
                catch (AssertionException)
                {
                    Thread.Sleep(100);
                }
            }

            throw new TimeoutException();
        }

        public override void Flush()
        {
        }
    }
}