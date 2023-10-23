using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Communications
{
    public class KnownService
    {
        /// <summary>
        /// The <see cref="Type"/> of the concrete service implementation
        /// </summary>
        public Type ServiceImplementationType { get; }

        /// <summary>
        /// The <see cref="Type"/> of the service contract interface. The implementation may not necessarily implement this interface in the case of asynchronous services
        /// </summary>
        public Type ServiceContractType { get; }

        public KnownService(Type serviceImplementationType, Type serviceContractType)
        {
            //the implementation type doesn't need to implement the service contract, but it's _implied_ that it should have _some_ interfaces
            if (serviceImplementationType.IsInterface || serviceImplementationType.GetInterfaces().IsNullOrEmpty())
            {
                throw new InvalidServiceTypeException(serviceImplementationType);
            }

            ServiceImplementationType = serviceImplementationType;
            ServiceContractType = serviceContractType;
        }
    }
}