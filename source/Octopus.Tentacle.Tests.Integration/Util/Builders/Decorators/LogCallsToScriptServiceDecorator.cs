using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToScriptServiceDecorator : IAsyncClientScriptService
    {
        private readonly IAsyncClientScriptService inner;
        private readonly ILogger logger;

        public LogCallsToScriptServiceDecorator(IAsyncClientScriptService inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceDecorator>();
        }

        public async Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, HalibutProxyRequestOptions options)
        {
            logger.Information("StartScript call started");
            try
            {
                return await inner.StartScriptAsync(command, options);
            }
            finally
            {
                logger.Information("StartScript call complete");
            }
        }

        public async Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, HalibutProxyRequestOptions options)
        {
            logger.Information("GetStatus call started");
            try
            {
                return await inner.GetStatusAsync(request, options);
            }
            finally
            {
                logger.Information("GetStatus call complete");
            }
        }

        public async Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, HalibutProxyRequestOptions options)
        {
            logger.Information("CancelScript call started");
            try
            {
                return await inner.CancelScriptAsync(command, options);
            }
            finally
            {
                logger.Information("CancelScript call complete");
            }
        }

        public async Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, HalibutProxyRequestOptions options)
        {
            logger.Information("CompleteScript call started");
            try
            {
                return await inner.CompleteScriptAsync(command, options);
            }
            finally
            {
                logger.Information("CompleteScript call complete");
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderLogCallsToScriptServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder LogCallsToScriptService(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder)
        {
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(inner => new LogCallsToScriptServiceDecorator(inner));
        }
    }
}