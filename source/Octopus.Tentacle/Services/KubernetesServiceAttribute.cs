using System;

namespace Octopus.Tentacle.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public class KubernetesServiceAttribute : Attribute, IServiceAttribute
    {
        public Type ContractType { get; }

        public KubernetesServiceAttribute(Type contractType)
        {
            ContractType = contractType;
        }
    }
}