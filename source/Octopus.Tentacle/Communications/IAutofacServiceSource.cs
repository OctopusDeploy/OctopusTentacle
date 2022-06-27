using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Communications
{
    public interface IAutofacServiceSource
    {
        IEnumerable<Type> ServiceTypes { get; }
    }
}