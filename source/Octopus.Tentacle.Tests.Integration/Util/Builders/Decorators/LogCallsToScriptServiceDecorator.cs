using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToScriptServiceDecorator : IClientScriptService
    {
        private readonly IClientScriptService inner;
        private readonly ILogger logger;

        public LogCallsToScriptServiceDecorator(IClientScriptService inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceDecorator>();
        }

        public ScriptTicket StartScript(StartScriptCommand command, HalibutProxyRequestOptions options)
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

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request, HalibutProxyRequestOptions options)
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

        public ScriptStatusResponse CancelScript(CancelScriptCommand command, HalibutProxyRequestOptions options)
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

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command, HalibutProxyRequestOptions options)
        {
            logger.Information("CompleteScript call started");
            try
            {
                return inner.CompleteScript(command, options);
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