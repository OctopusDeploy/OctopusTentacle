using System;

namespace Octopus.Shared.Util
{
    public static class CommonValidationMessages
    {
        public static class MachinePolicy
        {
            public static readonly string DefaultPolicyMissingText = "Failed to find the default machine policy (Octopus needs this to exist as a fallback policy for machines).";
        }
    }
}