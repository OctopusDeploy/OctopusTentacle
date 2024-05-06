using System;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public interface IKubernetesScriptServiceV1
    {
        KubernetesScriptStatusResponseV1 StartScript(StartKubernetesScriptCommandV1 command);
        KubernetesScriptStatusResponseV1 GetStatus(KubernetesScriptStatusRequestV1 request);
        KubernetesScriptStatusResponseV1 CancelScript(CancelKubernetesScriptCommandV1 command);
        void CompleteScript(CompleteKubernetesScriptCommandV1 command);
    }
}