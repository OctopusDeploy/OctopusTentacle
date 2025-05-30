﻿using System;
using Octopus.Tentacle.Core.Services;

namespace Octopus.Tentacle.Services
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class KubernetesServiceAttribute : Attribute, IServiceAttribute
    {
        public KubernetesServiceAttribute(Type contractType)
        {
            ContractType = contractType;
        }

        public Type ContractType { get; }
    }
}