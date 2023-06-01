using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
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

    public class ErrorRecordingScriptServiceDecorator : IClientScriptService
    {
        private readonly ScriptServiceExceptions errors;
        private readonly IClientScriptService inner;
        private readonly ILogger logger;

        public ErrorRecordingScriptServiceDecorator(IClientScriptService inner, ScriptServiceExceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceDecorator>();
        }

        public ScriptTicket StartScript(StartScriptCommand command, HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.StartScript(command, options);
            }
            catch (Exception e)
            {
                errors.StartScriptLatestException = e;
                logger.Information(e, "Recorded error from StartScript");
                throw;
            }
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request, HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.GetStatus(request, options);
            }
            catch (Exception e)
            {
                errors.GetStatusLatestException = e;
                logger.Information(e, "Recorded error from GetStatus");
                throw;
            }
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command, HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.CancelScript(command, options);
            }
            catch (Exception e)
            {
                errors.CancelScriptLatestException = e;
                logger.Information(e, "Recorded error from CancelScript");
                throw;
            }
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command, HalibutProxyRequestOptions options)
        {
            try
            {
                return inner.CompleteScript(command, options);
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