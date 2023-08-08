using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CapabilitiesServiceV2Exceptions
    {
        public Exception? GetCapabilitiesLatestException { get; set; }
    }

    public class ErrorRecordingCapabilitiesServiceV2Decorator : IAsyncClientCapabilitiesServiceV2
    {
        private CapabilitiesServiceV2Exceptions errors;
        private IAsyncClientCapabilitiesServiceV2 inner;
        private ILogger logger;

        public ErrorRecordingCapabilitiesServiceV2Decorator(IAsyncClientCapabilitiesServiceV2 inner, CapabilitiesServiceV2Exceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingCapabilitiesServiceV2Decorator>();
        }

        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.GetCapabilitiesAsync(options);
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