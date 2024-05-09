using System;

namespace Octopus.Tentacle.Contracts.Logging
{
    public interface ITentacleTaskLog
    {
        void Info(string message);
        void Verbose(string message);
        void Verbose(Exception exception);
        void Warn(string message);
        void Warn(Exception exception, string message);
    }
}