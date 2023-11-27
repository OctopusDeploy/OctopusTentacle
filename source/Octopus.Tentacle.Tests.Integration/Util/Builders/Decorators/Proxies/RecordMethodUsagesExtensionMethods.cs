using System;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public static class RecordMethodUsagesExtensionMethods
    {
        public static IRecordedMethodUsage ForGetCapabilitiesAsync(this IRecordedMethodUsages o)
        {
            return o.For("GetCapabilitiesAsync");
        }

        public static IRecordedMethodUsage ForStartScriptAsync(this IRecordedMethodUsages o)
        {
            return o.For("StartScriptAsync");
        }

        public static IRecordedMethodUsage ForGetStatusAsync(this IRecordedMethodUsages o)
        {
            return o.For("GetStatusAsync");
        }

        public static IRecordedMethodUsage ForCancelScriptAsync(this IRecordedMethodUsages o)
        {
            return o.For("CancelScriptAsync");
        }

        public static IRecordedMethodUsage ForCompleteScriptAsync(this IRecordedMethodUsages o)
        {
            return o.For("CompleteScriptAsync");
        }
    }
}