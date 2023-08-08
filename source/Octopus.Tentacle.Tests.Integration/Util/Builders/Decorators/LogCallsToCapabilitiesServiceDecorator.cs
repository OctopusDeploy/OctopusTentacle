using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToCapabilitiesServiceDecorator : IAsyncClientCapabilitiesServiceV2
    {
        private IAsyncClientCapabilitiesServiceV2 inner;
        private ILogger logger;

        public LogCallsToCapabilitiesServiceDecorator(IAsyncClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToCapabilitiesServiceDecorator>();
        }

        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions options)
        {
            logger.Information("GetCapabilities call started");
            try
            {
                return await inner.GetCapabilitiesAsync(options);
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