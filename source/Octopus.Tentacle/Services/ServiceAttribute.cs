﻿using System;

namespace Octopus.Tentacle.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute, IServiceAttribute
    {
        public Type ContractType { get; }

        public ServiceAttribute(Type contractType)
        {
            ContractType = contractType;
        }
    }
}