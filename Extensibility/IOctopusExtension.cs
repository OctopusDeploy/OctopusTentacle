using System;
using System.Collections.Generic;
using System.Text;
using Autofac;

namespace Octopus.Shared.Extensibility
{
    public interface IOctopusExtension
    {
        void Load(ContainerBuilder builder);
    }
}
