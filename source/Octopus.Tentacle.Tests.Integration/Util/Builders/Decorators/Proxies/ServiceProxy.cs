using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public abstract class ServiceProxy : DispatchProxyAsync
    {
        protected object? TargetService { get; private set; }

        protected void SetTargetService(object service)
        {
            TargetService = service;
        }

        public override object Invoke(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            try
            {
                OnStartingInvocation(method);

                return method.Invoke(TargetService, args)!;
            }
            catch (TargetInvocationException e)
            {
                OnInvocationException(method, e.InnerException!);

                //we need to unwrap the TargetInvocationException
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                //this will never be hit because the line above will throw an exception
                return default;
            }
            catch (Exception e)
            {
                OnInvocationException(method, e);
                throw;
            }
            finally
            {
                OnCompletingInvocation(method, null);
            }
        }

        public override async Task InvokeAsync(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            try
            {
                await OnStartingInvocationAsync(method).ConfigureAwait(false);

                var task = (Task)method.Invoke(TargetService, args);

                await task!.ConfigureAwait(false);
            }
            catch (TargetInvocationException e)
            {
                OnInvocationException(method, e.InnerException!);

                //we need to unwrap the TargetInvocationException
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
            catch (Exception e)
            {
                OnInvocationException(method, e);
                throw;
            }
            finally
            {
                await OnCompletingInvocationAsync(method, null).ConfigureAwait(false);
            }
        }

        public override async Task<T> InvokeAsyncT<T>(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            T result = default;
            try
            {
                await OnStartingInvocationAsync(method).ConfigureAwait(false);

                var task = (Task<T>)method.Invoke(TargetService, args);

                result = await task!.ConfigureAwait(false);

                return result;
            }
            catch (TargetInvocationException e)
            {
                OnInvocationException(method, e.InnerException!);

                //we need to unwrap the TargetInvocationException
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                //this will never be hit because the line above will throw an exception
                return default;
            }
            catch (Exception e)
            {
                OnInvocationException(method, e);
                throw;
            }
            finally
            {
                await OnCompletingInvocationAsync(method, result).ConfigureAwait(false);
            }
        }

        void EnsureTargetServiceNotNull()
        {
            if (TargetService is null)
                throw new InvalidOperationException("TargetService has not been set via SetTargetService().");
        }

        protected abstract void OnStartingInvocation(MethodInfo targetMethod);
        protected abstract Task OnStartingInvocationAsync(MethodInfo targetMethod);
        protected abstract void OnCompletingInvocation(MethodInfo targetMethod, object? response);
        protected abstract Task OnCompletingInvocationAsync(MethodInfo targetMethod, object? response);
        protected abstract void OnInvocationException(MethodInfo targetMethod, Exception exception);
    }
}
