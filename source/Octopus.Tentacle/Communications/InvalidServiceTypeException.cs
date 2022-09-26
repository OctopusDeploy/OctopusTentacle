﻿using System;

namespace Octopus.Tentacle.Communications
{
    public class InvalidServiceTypeException : Exception
    {
        public InvalidServiceTypeException(Type invalidType)
            : base($"Error: {invalidType.Name} must be a class that implements an interface")
        {
            InvalidType = invalidType;
        }

        public Type InvalidType { get; }
    }
}