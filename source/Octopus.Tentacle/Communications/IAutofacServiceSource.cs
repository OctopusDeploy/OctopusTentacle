using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Communications
{
    public interface IAutofacServiceSource
    {
        IEnumerable<KnownService> KnownServices { get; }
    }
}