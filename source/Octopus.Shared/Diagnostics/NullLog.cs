using System;
using System.Collections.Generic;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class NullLog : AbstractLog
    {
        readonly LogContext currentContext;

        public NullLog()
        {
            currentContext = new LogContext("Null");
        }

        public override ILogContext CurrentContext
        {
            get { return currentContext; }
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
        }

        public override IDisposable WithinBlock(ILogContext logContext)
        {
            return new NullDisposable();
        }

        public override void Flush()
        {
        }

        public class NullDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}