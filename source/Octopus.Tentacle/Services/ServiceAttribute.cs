using System;

namespace Octopus.Tentacle.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
        public Type ServiceInterfaceType { get; }

        public ServiceAttribute(Type serviceInterfaceType)
        {
            ServiceInterfaceType = serviceInterfaceType;
        }
    }
}