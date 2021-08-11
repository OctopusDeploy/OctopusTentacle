using System;

namespace Octopus.Shared.Communications
{
    public class UnknownServiceNameException : Exception
    {
        public UnknownServiceNameException(string serviceName)
            : base($"Error: {serviceName} has not been registered through an IAutofacServiceSource")
        {
            ServiceName = serviceName;
        }
        
        public string ServiceName { get; }
    }
}