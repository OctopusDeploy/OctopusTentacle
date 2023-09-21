using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class LogExtensionMethods
    {
        public static Log Chain(this Log log, Log? otherLog)
        {
            if (otherLog == null)
            {
                return log;
            }

            return new LogChain(log, otherLog);
        }

        class LogChain : Log
        {
            private readonly Log log1;
            private readonly Log log2;

            public LogChain(Log log1, Log log2)
            {
                this.log1 = log1;
                this.log2 = log2;
            }

            public override string CorrelationId => log1.CorrelationId;

            public override void Write(LogCategory category, Exception? error, string messageText)
            {
                log1.Write(category, error, messageText);
                log2.Write(category, error, messageText);
            }
        }
    }
}