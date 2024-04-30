using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public class KubernetesScriptStatusRequestV1
    {
        public KubernetesScriptStatusRequestV1(ScriptTicket scriptTicket, long lastLogSequence)
        {
            ScriptTicket = scriptTicket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        public long LastLogSequence { get; }
    }
}