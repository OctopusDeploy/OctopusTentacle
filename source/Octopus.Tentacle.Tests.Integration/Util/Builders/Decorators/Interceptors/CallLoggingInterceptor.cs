using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public class CallLoggingInterceptor : IAsyncInterceptor
    {
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<CallLoggingInterceptor>();

        public void InterceptSynchronous(IInvocation invocation)
        {
            logger.Information("{MethodName} call started (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
            try
            {
                invocation.Proceed();
            }
            catch (Exception e)
            {
                logger.Information(e, "{MethodName} threw an exception (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
                throw;
            }
            finally
            {
                logger.Information("{MethodName} call completed (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
            }
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
        }

        async Task InternalInterceptAsynchronous(IInvocation invocation)
        {
            try
            {
                logger.Information("{MethodName} call started (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());

                invocation.Proceed();

                await (Task)invocation.ReturnValue;
            }
            catch (Exception e)
            {
                logger.Information(e, "{MethodName} threw an exception (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
                throw;
            }
            finally
            {
                logger.Information("{MethodName} call completed (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
            }
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(invocation);
        }

        async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
        {
            try
            {
                logger.Information("{MethodName} call started (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());

                invocation.Proceed();

                return await (Task<TResult>)invocation.ReturnValue;
            }
            catch (Exception e)
            {
                logger.Information(e, "{MethodName} threw an exception (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
                throw;
            }
            finally
            {
                logger.Information("{MethodName} call completed (Invocation: {InvocationId})", invocation.Method.Name, invocation.GetHashCode());
            }
        }
    }
}