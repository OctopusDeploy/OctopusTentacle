using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToScriptServiceV2Decorator : IClientScriptServiceV2
    {
        private IClientScriptServiceV2 inner;
        private ILogger logger;

        public LogCallsToScriptServiceV2Decorator(IClientScriptServiceV2 inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToScriptServiceV2Decorator>();
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            logger.Information("StartScript call started");
            try
            {
                return inner.StartScript(command, options);
            }
            finally
            {
                logger.Information("StartScript call complete");
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions options)
        {
            logger.Information("GetStatus call started");
            try
            {
                return inner.GetStatus(request, options);
            }
            finally
            {
                logger.Information("GetStatus call complete");
            }
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            logger.Information("CancelScript call started");
            try
            {
                return inner.CancelScript(command, options);
            }
            finally
            {
                logger.Information("CancelScript call complete");
            }
        }

        public void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            logger.Information("CompleteScript call started");
            try
            {
                inner.CompleteScript(command, options);
            }
            finally
            {
                logger.Information("CompleteScript call complete");
            }

        }
    }

    public static class TentacleServiceDecoratorBuilderLogCallsToScriptServiceV2DecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder LogCallsToScriptServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder)
        {
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(inner => new LogCallsToScriptServiceV2Decorator(inner));
        }
    }
}