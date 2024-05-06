using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public class CancelKubernetesScriptCommandV1
    {
        public CancelKubernetesScriptCommandV1(ScriptTicket scriptTicket, long lastLogSequence)
        {
            ScriptTicket = scriptTicket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        public long LastLogSequence { get; }
    }
}