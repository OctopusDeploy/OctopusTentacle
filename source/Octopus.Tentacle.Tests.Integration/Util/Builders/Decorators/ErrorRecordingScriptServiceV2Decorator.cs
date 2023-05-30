using System;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV2Exceptions
    {
        public Exception? StartScriptLatestException { get; set; }
        public Exception? GetStatusLatestException { get; set; }
        public Exception? CancelScriptLatestException { get; set; }
        public Exception? CompleteScriptLatestException { get; set; }
    }

    public class ErrorRecordingScriptServiceV2Decorator : IScriptServiceV2
    {
        private ScriptServiceV2Exceptions errors;
        private IScriptServiceV2 inner;
        private ILogger logger;

        public ErrorRecordingScriptServiceV2Decorator(IScriptServiceV2 inner, ScriptServiceV2Exceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceV2Decorator>();
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
        {
            try
            {
                return inner.StartScript(command);
            }
            catch (Exception e)
            {
                errors.StartScriptLatestException = e;
                logger.Information(e, "Recorded error from StartScript");
                throw;
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            try
            {
                return inner.GetStatus(request);
            }
            catch (Exception e)
            {
                errors.GetStatusLatestException = e;
                logger.Information(e, "Recorded error from GetStatus");
                throw;
            }
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            try
            {
                return inner.CancelScript(command);
            }
            catch (Exception e)
            {
                errors.CancelScriptLatestException = e;
                logger.Information(e, "Recorded error from CancelScript");
                throw;
            }
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            try
            {
                inner.CompleteScript(command);
            }
            catch (Exception e)
            {
                errors.CompleteScriptLatestException = e;
                logger.Information(e, "Recorded error from CompleteScript");
                throw;
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderErrorRecordingScriptServiceV2DecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder RecordExceptionThrownInScriptServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out ScriptServiceV2Exceptions scriptServiceV2Exceptions)
        {
            var myScriptServiceV2Exceptions = new ScriptServiceV2Exceptions();
            scriptServiceV2Exceptions = myScriptServiceV2Exceptions;
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(inner => new ErrorRecordingScriptServiceV2Decorator(inner, myScriptServiceV2Exceptions));
        }
    }
}