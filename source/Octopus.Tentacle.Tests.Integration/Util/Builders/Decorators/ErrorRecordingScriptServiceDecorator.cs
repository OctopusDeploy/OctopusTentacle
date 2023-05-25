using System;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceExceptions
    {
        public Exception? StartScriptLatestException;
        public Exception? GetStatusLatestException { get; set; }
        public Exception? CancelScriptLatestException { get; set; }
        
        public Exception? CompleteScriptLatestException { get; set; }
    }

    public class ErrorRecordingScriptServiceDecorator : IScriptService
    {
        private readonly ScriptServiceExceptions errors;
        private readonly IScriptService inner;
        private readonly ILogger logger;

        public ErrorRecordingScriptServiceDecorator(IScriptService inner, ScriptServiceExceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceDecorator>();
        }

        public ScriptTicket StartScript(StartScriptCommand command)
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

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
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

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
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

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            try
            {
                return inner.CompleteScript(command);
            }
            catch (Exception e)
            {
                errors.CompleteScriptLatestException = e;
                logger.Information(e, "Recorded error from CompleteScript");
                throw;
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderErrorRecordingScriptServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder RecordExceptionThrownInScriptService(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out ScriptServiceExceptions scriptServiceExceptions)
        {
            var myScriptServiceExceptions = new ScriptServiceExceptions();
            scriptServiceExceptions = myScriptServiceExceptions;
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(inner => new ErrorRecordingScriptServiceDecorator(inner, myScriptServiceExceptions));
        }
    }
}