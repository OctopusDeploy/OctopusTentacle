using System;
using System.Collections.Generic;

namespace Octopus.Shared.Diagnostics
{
    public class NullLog : AbstractLog
    {
        readonly LogContext currentContext;

        public NullLog()
        {
            currentContext = LogContext.CreateNew("Null");
        }

        public override LogContext CurrentContext
        {
            get { return currentContext; }
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
        }

        public override IDisposable WithinBlock(LogContext logContext)
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