using System;
using System.Diagnostics;

namespace Octopus.Tentacle.Contracts.Logging
{
    public interface ITentacleClientTaskLog
    {
        void Info(string message);
        void Verbose(string message);
        void Verbose(Exception exception);
        void Warn(string message);
        void Warn(Exception exception, string message);
    }

    public static class TentacleClientTaskLogExtensions
    {
        public static void VerboseTimed(this ITentacleClientTaskLog log, Stopwatch sw, string message)
        {
            log.Verbose($"{message} ({sw.ElapsedMilliseconds}ms elapsed)");
            sw.Reset();
        }
    }

    public class EmptyLog : ITentacleClientTaskLog
    {
        public void Info(string message)
        {
        }

        public void Verbose(string message)
        {
        }

        public void Verbose(Exception exception)
        {
        }

        public void Warn(string message)
        {
        }

        public void Warn(Exception exception, string message)
        {
        }
    }
}
