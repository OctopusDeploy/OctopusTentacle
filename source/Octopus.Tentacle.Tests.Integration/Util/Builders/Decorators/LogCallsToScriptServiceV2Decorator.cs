using System;
using System.Threading;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToScriptServiceV2Decorator : IScriptServiceV2
    {
        private IScriptServiceV2 inner;
        private ILogger logger;

        public LogCallsToScriptServiceV2Decorator(IScriptServiceV2 inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceV2Decorator>();
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
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

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
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

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
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

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            logger.Information("CompleteScript call started");
            try
            {
                inner.CompleteScript(command);
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