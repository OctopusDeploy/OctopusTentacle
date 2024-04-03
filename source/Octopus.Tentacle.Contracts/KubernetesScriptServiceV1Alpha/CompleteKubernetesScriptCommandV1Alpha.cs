using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public class CompleteKubernetesScriptCommandV1Alpha
    {
        public ScriptTicket ScriptTicket { get; }

        public CompleteKubernetesScriptCommandV1Alpha(ScriptTicket scriptTicket)
        {
            ScriptTicket = scriptTicket;
        }
    }
}