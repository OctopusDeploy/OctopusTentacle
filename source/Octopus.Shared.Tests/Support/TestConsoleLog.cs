using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tests.Support
{
    public class TestConsoleLog : AbstractLog
    {
        public override ILogContext CurrentContext => new LogContext();

        protected override void WriteEvent(LogEvent logEvent)
        {
            Console.WriteLine($"{DateTime.Now} {Thread.CurrentThread.ManagedThreadId} {logEvent.Category} {logEvent.MessageText}");
            if (logEvent.Error != null) Console.WriteLine(logEvent.Error);
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
            throw new NotImplementedException();
        }

        public override IDisposable WithinBlock(ILogContext logContext)
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
    }
}