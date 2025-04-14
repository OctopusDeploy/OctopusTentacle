using System;
using System.Threading;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Core.Services.Scripts
{
    public static class ScriptTicketFactory
    {
        static long nextTaskId;
        public static ScriptTicket Create(string? serverTaskId)
        {
            serverTaskId = serverTaskId?.Replace("ServerTasks-", string.Empty);
            return new ScriptTicket($"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{serverTaskId}-{Interlocked.Increment(ref nextTaskId)}");
        }
    }
}