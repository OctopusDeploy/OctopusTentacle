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
            try
            {
                await OnStartingInvocationAsync(invocation);

                invocation.Proceed();

                await ((Task)invocation.ReturnValue).ConfigureAwait(false);
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
            try
            {
                await OnStartingInvocationAsync(invocation);

                invocation.Proceed();

                return await ((Task<TResult>)invocation.ReturnValue).ConfigureAwait(false);
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

        protected virtual void OnStartingInvocation(IInvocation invocation)
        {}

        protected virtual Task OnStartingInvocationAsync(IInvocation invocation)
        {
            OnStartingInvocation(invocation);
            return Task.CompletedTask;
        }

        protected virtual void OnCompletingInvocation(IInvocation invocation)
        {
        }

        protected virtual Task OnCompletingInvocationAsync(IInvocation invocation)
        {
            OnCompletingInvocation(invocation);
            return Task.CompletedTask;
        }

        protected virtual void OnInvocationException(IInvocation invocation, Exception exception)
        {
        }

        protected virtual Task OnInvocationExceptionAsync(IInvocation invocation, Exception exception)
        {
            OnInvocationException(invocation, exception);
            return Task.CompletedTask;
        }
    }
}