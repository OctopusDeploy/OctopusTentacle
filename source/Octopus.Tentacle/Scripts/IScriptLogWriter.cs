using System;
using Octopus.Shared.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptLogWriter : IDisposable
    {
        void WriteOutput(ProcessOutputSource source, string message);
    }
}