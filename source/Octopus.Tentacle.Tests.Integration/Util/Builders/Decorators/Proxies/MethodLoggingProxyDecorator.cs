using System;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class MethodLoggingProxyDecorator<TService> : ServiceProxy where TService : class
    {
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<MethodLoggingProxyDecorator<TService>>();
        readonly string serviceTypeName = typeof(TService).Name;

        public static TService Create(TService service)
        {
            var proxy = DispatchProxyAsync.Create<TService, MethodLoggingProxyDecorator<TService>>();
            (proxy as MethodLoggingProxyDecorator<TService>)!.SetTargetService(service);
            return proxy;
        }

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            logger.Information("{ServiceName}.{MethodName} call started", serviceTypeName, targetMethod.Name);
        }

        protected override Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            logger.Information("{ServiceName}.{MethodName} call started", serviceTypeName, targetMethod.Name);
            return Task.CompletedTask;
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod)
        {
            logger.Information("{ServiceName}.{MethodName} call completed", serviceTypeName, targetMethod.Name);
        }

        protected override Task OnCompletingInvocationAsync(MethodInfo targetMethod)
        {
            logger.Information("{ServiceName}.{MethodName} call completed", serviceTypeName, targetMethod.Name);
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
            logger.Information(exception, "{ServiceName}.{MethodName} threw an exception", serviceTypeName, targetMethod.Name);
        }
    }
}