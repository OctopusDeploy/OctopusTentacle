using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public abstract class AsyncInterceptor: IAsyncInterceptor
    {
        readonly ILogger logger;

        protected AsyncInterceptor()
        {
            logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            logger.Information("[ENTRY] InterceptSynchronous");
            try
            {
                logger.Information("InterceptSynchronous.OnStartingInvocation()");
                OnStartingInvocation(invocation);

                logger.Information("InterceptSynchronous.invocation.Proceed()");
                invocation.Proceed();
            }
            catch (Exception e)
            {
                logger.Information("InterceptSynchronous.OnInvocationException()");
                OnInvocationException(invocation, e);
                throw;
            }
            finally
            {
                logger.Information("InterceptSynchronous.OnCompletingInvocation()");

                OnCompletingInvocation(invocation);

                logger.Information("[EXIT] InterceptSynchronous");
            }

        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            logger.Information("[ENTRY] InterceptAsynchronous.ReturnValue: {ReturnValue}", invocation.ReturnValue);
            invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
            logger.Information("[EXIT] InterceptAsynchronous.ReturnValue: {ReturnValue}", invocation.ReturnValue);
        }

        async Task InternalInterceptAsynchronous(IInvocation invocation)
        {
            logger.Information("[ENTRY] InternalInterceptAsynchronous");
            var proceedInfo = invocation.CaptureProceedInfo();
            try
            {
                logger.Information("InternalInterceptAsynchronous.OnStartingInvocationAsync()");
                await OnStartingInvocationAsync(invocation).ConfigureAwait(false);

                logger.Information("InternalInterceptAsynchronous.proceedInfo.Invoke()");
                proceedInfo.Invoke();

                var task = (Task)invocation.ReturnValue;

                logger.Information("InternalInterceptAsynchronous.await Task");
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.Information("InternalInterceptAsynchronous.OnInvocationException()");
                OnInvocationException(invocation, e);
                throw;
            }
            finally
            {
                logger.Information("InternalInterceptAsynchronous.OnCompletingInvocationAsync()");

                await OnCompletingInvocationAsync(invocation).ConfigureAwait(false);

                logger.Information("[EXIT] InternalInterceptAsynchronous");
            }
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            logger.Information("[ENTRY] InterceptAsynchronous<TResult>.ReturnValue: {ReturnValue}", invocation.ReturnValue);
            var task = InternalInterceptAsynchronous<TResult>(invocation);
            invocation.ReturnValue = task;
            logger.Information("[EXIT] InterceptAsynchronous<TResult>.Task: {TaskId}", task.Id);
        }

        async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
        {
            logger.Information("[ENTRY] InternalInterceptAsynchronous<TResult>");

            var proceedInfo = invocation.CaptureProceedInfo();
            try
            {
                logger.Information("InternalInterceptAsynchronous<TResult>.OnStartingInvocationAsync()");
                await OnStartingInvocationAsync(invocation).ConfigureAwait(false);

                logger.Information("InternalInterceptAsynchronous<TResult>.proceedInfo.Invoke()");
                proceedInfo.Invoke();

                var task = (Task<TResult>)invocation.ReturnValue;

                logger.Information("InternalInterceptAsynchronous<TResult>.await Task: {TaskId}", task.Id);
                var result =  await task.ConfigureAwait(false);

                return result;
            }
            catch (Exception e)
            {
                logger.Information("InternalInterceptAsynchronous<TResult>.OnInvocationException()");
                OnInvocationException(invocation, e);
                throw;
            }
            finally
            {
                logger.Information("InternalInterceptAsynchronous<TResult>.OnCompletingInvocationAsync()");
                await OnCompletingInvocationAsync(invocation).ConfigureAwait(false);

                logger.Information("[EXIT] InternalInterceptAsynchronous<TResult>");
            }
        }

        protected abstract void OnStartingInvocation(IInvocation invocation);
        protected abstract Task OnStartingInvocationAsync(IInvocation invocation);
        protected abstract void OnCompletingInvocation(IInvocation invocation);
        protected abstract Task OnCompletingInvocationAsync(IInvocation invocation);
        protected abstract void OnInvocationException(IInvocation invocation, Exception exception);
    }
}