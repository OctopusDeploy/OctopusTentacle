using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
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

    public class ErrorRecordingScriptServiceDecorator : IAsyncClientScriptService
    {
        private readonly ScriptServiceExceptions errors;
        private readonly IAsyncClientScriptService inner;
        private readonly ILogger logger;

        public ErrorRecordingScriptServiceDecorator(IAsyncClientScriptService inner, ScriptServiceExceptions errors)
        {
            this.inner = inner;
            this.errors = errors;
            logger = new SerilogLoggerBuilder().Build().ForContext<ErrorRecordingScriptServiceDecorator>();
        }

        public async Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.StartScriptAsync(command, options);
            }
            catch (Exception e)
            {
                errors.StartScriptLatestException = e;
                logger.Information(e, "Recorded error from StartScript");
                throw;
            }
        }

        public async Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.GetStatusAsync(request, options);
            }
            catch (Exception e)
            {
                errors.GetStatusLatestException = e;
                logger.Information(e, "Recorded error from GetStatus");
                throw;
            }
        }

        public async Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.CancelScriptAsync(command, options);
            }
            catch (Exception e)
            {
                errors.CancelScriptLatestException = e;
                logger.Information(e, "Recorded error from CancelScript");
                throw;
            }
        }

        public async Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, HalibutProxyRequestOptions options)
        {
            try
            {
                return await inner.CompleteScriptAsync(command, options);
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