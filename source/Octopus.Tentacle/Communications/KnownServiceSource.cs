using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Communications
{
    public class KnownServiceSource : IAutofacServiceSource
    {
        public KnownServiceSource(params KnownService[] serviceTypes)
        {
            KnownServices = serviceTypes;
        }

        public IEnumerable<KnownService> KnownServices { get; }
    }

    public class KnownService
    {
        public Type ServiceImplementationType { get; }
        public Type ServiceInterfaceType { get; }

        public KnownService(Type serviceImplementationType, Type serviceInterfaceType)
        {
            ServiceImplementationType = serviceImplementationType;
            ServiceInterfaceType = serviceInterfaceType;
        }
    }
}