using System;
using System.Collections.Generic;

namespace Octopus.Shared.Communications
{
    public class KnownServiceSource : IAutofacServiceSource
    {
        public KnownServiceSource(params Type[] serviceTypes)
        {
            ServiceTypes = serviceTypes;
        }
        
        public IEnumerable<Type> ServiceTypes { get; }
    }
}