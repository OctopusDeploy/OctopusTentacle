using System;
using Halibut;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public static class HalibutRuntimeExtensionMethods
    {
        public static HalibutRuntimeBuilder WithLegacyContractSupport(this HalibutRuntimeBuilder builder)
        {
            return builder.WithMessageSerializer(s => s.WithLegacyContractSupport());
        }
    }
}