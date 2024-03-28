using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public interface IKubernetesScriptServiceV1Alpha
    {
        KubernetesScriptStatusResponseV1Alpha StartScript(StartKubernetesScriptCommandV1Alpha command);
        KubernetesScriptStatusResponseV1Alpha GetStatus(KubernetesScriptStatusRequestV1Alpha request);
        KubernetesScriptStatusResponseV1Alpha CancelScript(CancelKubernetesScriptCommandV1Alpha command);
        void CompleteScript(CompleteKubernetesScriptCommandV1Alpha command);
    }
}