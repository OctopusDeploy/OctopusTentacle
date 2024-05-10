using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class LogExtensionMethods
    {
        public static ITentacleClientTaskLog Chain(this ITentacleClientTaskLog log, ITentacleClientTaskLog? otherLog)
        {
            if (otherLog == null)
            {
                return log;
            }

            return new LogChain(log, otherLog);
        }

        class LogChain : ITentacleClientTaskLog
        {
            private readonly ITentacleClientTaskLog log1;
            private readonly ITentacleClientTaskLog log2;

            public LogChain(ITentacleClientTaskLog log1, ITentacleClientTaskLog log2)
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