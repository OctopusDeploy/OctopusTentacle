using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class LogCallsToScriptServiceV2Decorator : IAsyncClientScriptServiceV2
    {
        private IAsyncClientScriptServiceV2 inner;
        private ILogger logger;

        public LogCallsToScriptServiceV2Decorator(IAsyncClientScriptServiceV2 inner)
        {
            this.inner = inner;
            logger = new SerilogLoggerBuilder().Build().ForContext<LogCallsToScriptServiceV2Decorator>();
        }

        public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions options)
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

        public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions options)
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

        public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions options)
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

        public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            logger.Information("CompleteScript call started");
            try
            {
                await inner.CompleteScriptAsync(command, options);
            }
            finally
            {
                logger.Information("CompleteScript call complete");
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderLogCallsToScriptServiceV2DecoratorExtensionMethods
    {
        private static readonly ProxyGenerator Generator = new();

        public static TentacleServiceDecoratorBuilder LogCallsToScriptServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder)
        {
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(inner => Generator.CreateInterfaceProxyWithTarget(inner, new CallLoggingInterceptor()));
        }
    }
}