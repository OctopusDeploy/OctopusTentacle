using System;

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
            ServiceImplementationType = serviceImplementationType;
            ServiceContractType = serviceContractType;
        }
    }
}