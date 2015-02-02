using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Diagnostics
{
    public class NullLog : AbstractLog
    {
        LogCorrelator current;

        public NullLog()
        {
            current = LogCorrelator.CreateNew("Null");
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
        }

        public override IDisposable WithinBlock(LogCorrelator logger)
        {
            return new NullDisposable();
        }

        public override LogCorrelator Current
        {
            get { return current; }
        }

        public class NullDisposable : IDisposable
        {
            public void Dispose()
            {
                
            }
        }
    }
}