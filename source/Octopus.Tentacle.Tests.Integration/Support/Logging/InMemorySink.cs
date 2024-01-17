using System;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace Octopus.Tentacle.Tests.Integration.Support.Logging
{
    public class InMemorySink : ILogEventSink, IDisposable
    {
        private readonly List<LogEvent> logEvents;

        public InMemorySink() : this(new List<LogEvent>())
        {
        }

        protected InMemorySink(List<LogEvent> logEvents)
        {
            this.logEvents = logEvents;
        }

        public IEnumerable<LogEvent> LogEvents => logEvents.AsReadOnly();

        public void Dispose()
        {
            logEvents.Clear();
        }

        public virtual void Emit(LogEvent logEvent)
        {
            logEvents.Add(logEvent);
        }
    }
}