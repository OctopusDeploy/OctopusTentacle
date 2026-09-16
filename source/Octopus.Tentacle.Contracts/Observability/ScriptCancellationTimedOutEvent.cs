using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public record ScriptCancellationTimedOutEvent(
        ScriptTicket ScriptTicket,
        string TaskId,
        ScriptIsolationLevel IsolationLevel,
        string MutexName,
        TimeSpan ScriptCancellationTimeoutBeforeAbandoning);
}