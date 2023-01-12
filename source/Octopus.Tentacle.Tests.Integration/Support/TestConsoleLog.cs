using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TestConsoleLog : Log
    {
        public override bool IsEnabled(LogCategory category)
            => true;

        protected override void WriteEvent(LogEvent logEvent)
        {
            Console.WriteLine($"{DateTime.Now} {Thread.CurrentThread.ManagedThreadId} {logEvent.Category} {logEvent.MessageText}");
            if (logEvent.Error != null) Console.WriteLine(logEvent.Error);
        }

        public override void Flush()
        {
        }

        public override string CorrelationId => "system/" + Environment.MachineName;
    }
}