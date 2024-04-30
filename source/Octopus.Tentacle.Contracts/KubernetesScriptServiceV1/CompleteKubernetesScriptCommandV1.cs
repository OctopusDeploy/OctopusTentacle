using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public class CompleteKubernetesScriptCommandV1
    {
        public ScriptTicket ScriptTicket { get; }

        public CompleteKubernetesScriptCommandV1(ScriptTicket scriptTicket)
        {
            ScriptTicket = scriptTicket;
        }
    }
}