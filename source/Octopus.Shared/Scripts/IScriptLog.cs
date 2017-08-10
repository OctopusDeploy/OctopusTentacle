using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Scripts
{
    public interface IScriptLog
    {
        IScriptLogWriter CreateWriter();
        List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber);
    }
}