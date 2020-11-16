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

        public override ILogContext CurrentContext => currentContext;

        protected override void WriteEvent(LogEvent logEvent)
        {
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
        }

        public override IDisposable WithinBlock(ILogContext logContext)
            => new NullDisposable();

        public override void Flush()
        {
        }

        public override void Flush(string correlationId)
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