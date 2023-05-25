using System;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToScriptServiceDecorator : IScriptService
    {
        private readonly IScriptService inner;
        private readonly ILogger logger;

        public LogCallsToScriptServiceDecorator(IScriptService inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceDecorator>();
        }

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            logger.Information("StartScript call started");
            try
            {
                return inner.StartScript(command);
            }
            finally
            {
                logger.Information("StartScript call complete");
            }
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            logger.Information("GetStatus call started");
            try
            {
                return inner.GetStatus(request);
            }
            finally
            {
                logger.Information("GetStatus call complete");
            }
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            logger.Information("CancelScript call started");
            try
            {
                return inner.CancelScript(command);
            }
            finally
            {
                logger.Information("CancelScript call complete");
            }
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            logger.Information("CompleteScript call started");
            try
            {
                return inner.CompleteScript(command);
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