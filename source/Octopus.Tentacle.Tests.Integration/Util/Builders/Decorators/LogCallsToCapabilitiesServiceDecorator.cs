using Octopus.Tentacle.Contracts.Capabilities;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToCapabilitiesServiceDecorator : ICapabilitiesServiceV2
    {
        private ICapabilitiesServiceV2 inner;
        private ILogger logger;

        public LogCallsToCapabilitiesServiceDecorator(ICapabilitiesServiceV2 inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToCapabilitiesServiceDecorator>();
        }

        public CapabilitiesResponseV2 GetCapabilities()
        {
            logger.Information("GetCapabilities call started");
            try
            {
                return inner.GetCapabilities();
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