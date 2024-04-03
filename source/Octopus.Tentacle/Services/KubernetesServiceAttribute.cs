using System;

namespace Octopus.Tentacle.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public class KubernetesServiceAttribute : Attribute, IServiceAttribute
    {
        public KubernetesServiceAttribute(Type contractType)
        {
            ContractType = contractType;
        }

        public Type ContractType { get; }
    }
}