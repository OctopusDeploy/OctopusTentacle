using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public class CallLoggingInterceptor : AsyncInterceptor
    {
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<CallLoggingInterceptor>();

        protected override void OnStartingInvocation(IInvocation invocation)
        {
            logger.Information("{MethodName} call started (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
        }

        protected override Task OnStartingInvocationAsync(IInvocation invocation)
        {
            logger.Information("{MethodName} call started (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(IInvocation invocation, Exception exception)
        {
            logger.Information(exception, "{MethodName} threw an exception (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
        }

        protected override void OnCompletingInvocation(IInvocation invocation)
        {
            logger.Information("{MethodName} call completed (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
        }

        protected override Task OnCompletingInvocationAsync(IInvocation invocation)
        {
            logger.Information("{MethodName} call completed (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
            return Task.CompletedTask;
        }
    }
}