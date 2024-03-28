using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public class KubernetesScriptStatusRequestV1Alpha
    {
        public KubernetesScriptStatusRequestV1Alpha(ScriptTicket scriptTicket, long lastLogSequence)
        {
            ScriptTicket = scriptTicket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        public long LastLogSequence { get; }
    }
}