using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    [ServiceContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1", Name = "Health")]
    public interface IHealthService
    {
        [OperationContract]
        HealthResult CheckHealth();
    }
}
