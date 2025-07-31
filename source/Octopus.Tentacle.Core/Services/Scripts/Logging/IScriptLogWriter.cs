using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Core.Services.Scripts.Logging
{
    public interface IScriptLogWriter : IDisposable
    {
        void WriteOutput(ProcessOutputSource source, string message);
        void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred);
    }
}