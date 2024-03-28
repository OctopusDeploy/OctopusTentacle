using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodLogService
    {
        Task<(IEnumerable<ProcessOutput>, long)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken);

        IKubernetesInMemoryLogWriter CreateWriter(ScriptTicket scriptTicket);
    }

    public interface IKubernetesInMemoryLogWriter
    {
        void WriteVerbose(ScriptTicket scriptTicket, string message);
        void WriteError(ScriptTicket scriptTicket, string message);
        void WriteInfo(ScriptTicket scriptTicket, string message);
    }
}