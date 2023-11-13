using System;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class MethodLoggingProxyDecorator : ServiceProxy
    {
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<MethodLoggingProxyDecorator>();
        string serviceTypeName;

        void Configure(string serviceTypeName)
        {
            this.serviceTypeName = serviceTypeName;
        }

        public static TService Create<TService>(TService service) where TService : class
        {
            var proxiedService = DispatchProxyAsync.Create<TService, MethodLoggingProxyDecorator>();
            var proxy = proxiedService as MethodLoggingProxyDecorator;
            proxy!.SetTargetService(service);
            proxy.Configure(typeof(TService).Name);

            return proxiedService;
        }

        protected override Task OnStartingInvocationAsync(MethodInfo targetMethod, object? request)
        {
            logger.Information("{ServiceName:l}.{MethodName:l}() started", serviceTypeName, targetMethod.Name);
            return Task.CompletedTask;
        }

        protected override Task OnCompletingInvocationAsync(MethodInfo targetMethod, object? response)
        {
            logger.Information("{ServiceName:l}.{MethodName:l}() completed", serviceTypeName, targetMethod.Name);
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
            logger.Information(exception, "{ServiceName:l}.{MethodName:l}() threw an exception", serviceTypeName, targetMethod.Name);
        }
    }
}