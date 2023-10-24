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
            if (serviceImplementationType.IsInterface || serviceImplementationType.IsAbstract || serviceImplementationType.GetInterfaces().IsNullOrEmpty())
            {
                throw new ArgumentException("The service implementation type must be a non-abstract class that implements at least one interface.", nameof(serviceImplementationType));
            }

            if (!serviceContractType.IsInterface)
            {
                throw new ArgumentException("The service contract type must be an interface.", nameof(serviceContractType));
            }

            ServiceImplementationType = serviceImplementationType;
            ServiceContractType = serviceContractType;
        }
    }
}