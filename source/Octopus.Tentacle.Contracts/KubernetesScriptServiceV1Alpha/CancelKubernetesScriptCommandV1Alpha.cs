using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public class CancelKubernetesScriptCommandV1Alpha
    {
        public CancelKubernetesScriptCommandV1Alpha(ScriptTicket scriptTicket, long lastLogSequence)
        {
            ScriptTicket = scriptTicket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        public long LastLogSequence { get; }
    }
}