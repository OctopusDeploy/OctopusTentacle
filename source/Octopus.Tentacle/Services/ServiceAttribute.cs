using System;

namespace Octopus.Tentacle.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
        public Type ServiceType { get; }

        public ServiceAttribute(Type serviceType)
        {
            ServiceType = serviceType;
        }
    }
}