using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptLogWriter : IDisposable
    {
        void WriteOutput(ProcessOutputSource source, string message);
        void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred);
    }
}