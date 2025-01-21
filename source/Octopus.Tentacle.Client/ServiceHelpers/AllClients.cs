using Halibut;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.ServiceHelpers
{
    public class AllClients
    {
        public IAsyncClientScriptService ScriptServiceV1 { get; }
        public IAsyncClientScriptServiceV2 ScriptServiceV2 { get; }
        public IAsyncClientKubernetesScriptServiceV1 KubernetesScriptServiceV1 { get; }
        public IAsyncClientFileTransferService ClientFileTransferServiceV1 { get; }
        public IAsyncClientCapabilitiesServiceV2 CapabilitiesServiceV2 { get; }

        public AllClients(IHalibutRuntime halibutRuntime, ServiceEndPoint serviceEndPoint) : this(halibutRuntime, serviceEndPoint, null)
        {
        }


        internal AllClients(IHalibutRuntime halibutRuntime, ServiceEndPoint serviceEndPoint, ITentacleServiceDecoratorFactory? tentacleServicesDecoratorFactory)
        {
            ScriptServiceV1 = halibutRuntime.CreateAsyncClient<IScriptService, IAsyncClientScriptService>(serviceEndPoint);
            ScriptServiceV2 = halibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(serviceEndPoint);
            KubernetesScriptServiceV1 = halibutRuntime.CreateAsyncClient<IKubernetesScriptServiceV1, IAsyncClientKubernetesScriptServiceV1>(serviceEndPoint);
            ClientFileTransferServiceV1 = halibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(serviceEndPoint);
            CapabilitiesServiceV2 = halibutRuntime.CreateAsyncClient<ICapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();

            if (tentacleServicesDecoratorFactory != null)
            {
                ScriptServiceV1 = tentacleServicesDecoratorFactory.Decorate(ScriptServiceV1);
                ScriptServiceV2 = tentacleServicesDecoratorFactory.Decorate(ScriptServiceV2);
                KubernetesScriptServiceV1 = tentacleServicesDecoratorFactory.Decorate(KubernetesScriptServiceV1);
                ClientFileTransferServiceV1 = tentacleServicesDecoratorFactory.Decorate(ClientFileTransferServiceV1);
                CapabilitiesServiceV2 = tentacleServicesDecoratorFactory.Decorate(CapabilitiesServiceV2);
            }
        }
    }
}