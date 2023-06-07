using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CapabilitiesServiceV2Exceptions
    {
        public Exception? GetCapabilitiesLatestException { get; set; }
    }

    public class ErrorRecordingCapabilitiesServiceV2Decorator : IClientCapabilitiesServiceV2
    {
        private CapabilitiesServiceV2Exceptions errors;
        private IClientCapabilitiesServiceV2 inner;
        private ILogger logger;

        public ErrorRecordingCapabilitiesServiceV2Decorator(IClientCapabilitiesServiceV2 inner, CapabilitiesServiceV2Exceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingCapabilitiesServiceV2Decorator>();
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.GetCapabilities(options);
            }
            catch (Exception e)
            {
                errors.GetCapabilitiesLatestException = e;
                logger.Information(e, "Recorded error from GetCapabilities");
                throw;
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderErrorRecordingCapabilitiesServiceV2DecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder RecordExceptionThrownInCapabilitiesServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out CapabilitiesServiceV2Exceptions capabilitiesServiceV2Exceptions)
        {
            var myCapabilitiesServiceV2Exceptions = new CapabilitiesServiceV2Exceptions();
            capabilitiesServiceV2Exceptions = myCapabilitiesServiceV2Exceptions;
            return tentacleServiceDecoratorBuilder.DecorateCapabilitiesServiceV2With(inner => new ErrorRecordingCapabilitiesServiceV2Decorator(inner, myCapabilitiesServiceV2Exceptions));
        }
    }
}