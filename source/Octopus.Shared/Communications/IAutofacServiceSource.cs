using System;
using System.Collections.Generic;

namespace Octopus.Shared.Communications
{
    public interface IAutofacServiceSource
    {
        IEnumerable<Type>? ServiceTypes { get; }
    }
}