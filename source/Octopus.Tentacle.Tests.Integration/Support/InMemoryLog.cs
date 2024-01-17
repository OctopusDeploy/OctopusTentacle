using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class InMemoryLog : SystemLog
    {
        readonly ILog log;
        readonly BlockingCollection<LogEvent> events = new(1000);

        public InMemoryLog() : this(null)
        {
        }

        public InMemoryLog(ILog? log)
        {
            this.log = log ?? new TestConsoleLog();
        }

        public IEnumerable<LogEvent> LogEvents => events.ToArray();

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