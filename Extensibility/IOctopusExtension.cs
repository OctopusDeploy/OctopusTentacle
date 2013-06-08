using System;
using Autofac;

namespace Octopus.Shared.Extensibility
{
    public interface IOctopusExtension
    {
        void Load(ContainerBuilder builder);
    }
}