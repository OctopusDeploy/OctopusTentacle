using System;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class CallLoggingProxy<TService> : ServiceProxy<TService>
    {
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<CallLoggingProxy<TService>>();

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            logger.Information("{MethodName} call started", targetMethod.Name);
        }

        protected override Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            logger.Information("{MethodName} call started", targetMethod.Name);
            return Task.CompletedTask;
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod)
        {
            logger.Information("{MethodName} call completed", targetMethod.Name);
        }

        protected override Task OnCompletingInvocationAsync(MethodInfo targetMethod)
        {
            logger.Information("{MethodName} call completed", targetMethod.Name);
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
            logger.Information(exception, "{MethodName} threw an exception", targetMethod.Name);
        }
    }
}