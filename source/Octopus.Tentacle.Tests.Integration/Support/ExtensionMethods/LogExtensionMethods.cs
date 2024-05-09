using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class LogExtensionMethods
    {
        public static ITentacleTaskLog Chain(this ITentacleTaskLog log, ITentacleTaskLog? otherLog)
        {
            if (otherLog == null)
            {
                return log;
            }

            return new LogChain(log, otherLog);
        }

        class LogChain : ITentacleTaskLog
        {
            private readonly ITentacleTaskLog log1;
            private readonly ITentacleTaskLog log2;

            public LogChain(ITentacleTaskLog log1, ITentacleTaskLog log2)
            {
                this.log1 = log1;
                this.log2 = log2;
            }

            public void Info(string message)
            {
                log1.Info(message);
                log2.Info(message);
            }

            public void Verbose(string message)
            {
                log1.Verbose(message);
                log2.Verbose(message);
            }

            public void Verbose(Exception exception)
            {
                log1.Verbose(exception);
                log2.Verbose(exception);
            }

            public void Warn(string message)
            {
                log1.Warn(message);
                log2.Warn(message);
            }

            public void Warn(Exception exception, string message)
            {
                log1.Warn(exception, message);
                log2.Warn(exception, message);
            }

        }
    }
}