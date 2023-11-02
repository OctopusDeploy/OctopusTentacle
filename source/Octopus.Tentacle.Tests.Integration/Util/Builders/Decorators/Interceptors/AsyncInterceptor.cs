using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public abstract class AsyncInterceptor: IAsyncInterceptor
    {
        public void InterceptSynchronous(IInvocation invocation)
        {
            try
            {
                OnStartingInvocation(invocation);

                invocation.Proceed();
            }
            catch (Exception e)
            {
                OnInvocationException(invocation, e);
                throw;
            }
            finally
            {
                OnCompletingInvocation(invocation);
            }
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
        }

        async Task InternalInterceptAsynchronous(IInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();
            try
            {
                await OnStartingInvocationAsync(invocation);

                proceedInfo.Invoke();

                var task = (Task)invocation.ReturnValue;

                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await OnInvocationExceptionAsync(invocation, e);
                throw;
            }
            finally
            {
                await OnCompletingInvocationAsync(invocation);
            }
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(invocation);
        }

        async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();
            try
            {
                await OnStartingInvocationAsync(invocation);

                proceedInfo.Invoke();

                var task = (Task<TResult>)invocation.ReturnValue;

                return await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await OnInvocationExceptionAsync(invocation, e);
                throw;
            }
            finally
            {
                await OnCompletingInvocationAsync(invocation);
            }
        }

        protected abstract void OnStartingInvocation(IInvocation invocation);
        protected abstract Task OnStartingInvocationAsync(IInvocation invocation);
        protected abstract void OnCompletingInvocation(IInvocation invocation);
        protected abstract Task OnCompletingInvocationAsync(IInvocation invocation);
        protected abstract void OnInvocationException(IInvocation invocation, Exception exception);
        protected abstract Task OnInvocationExceptionAsync(IInvocation invocation, Exception exception);
    }
}