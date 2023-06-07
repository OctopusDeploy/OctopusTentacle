using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToCapabilitiesServiceDecorator : IClientCapabilitiesServiceV2
    {
        private IClientCapabilitiesServiceV2 inner;
        private ILogger logger;

        public LogCallsToCapabilitiesServiceDecorator(IClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToCapabilitiesServiceDecorator>();
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions options)
        {
            logger.Information("GetCapabilities call started");
            try
            {
                return inner.GetCapabilities(options);
            }
            finally
            {
                logger.Information("GetCapabilities call complete");
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderLogCallsToCapabilitiesServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder LogCallsToCapabilitiesServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder)
        {
            return tentacleServiceDecoratorBuilder.DecorateCapabilitiesServiceV2With(inner => new LogCallsToCapabilitiesServiceDecorator(inner));
        }
    }
}