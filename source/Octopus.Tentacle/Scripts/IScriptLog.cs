using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptLog
    {
        IScriptLogWriter CreateWriter();
        Task<(List<ProcessOutput> Outputs, long NextSequenceNumber)> GetOutput(long afterSequenceNumber);
    }
}