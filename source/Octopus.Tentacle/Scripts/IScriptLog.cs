using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptLog
    {
        IScriptLogWriter CreateWriter();
        List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber);
        string LogFilePath { get; }
    }
}