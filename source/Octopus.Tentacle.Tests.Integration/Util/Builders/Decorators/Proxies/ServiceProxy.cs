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
                OnCompletingInvocation(method);
            }
        }

        public override async Task InvokeAsync(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            try
            {
                await OnStartingInvocationAsync(method);

                await (Task)method.Invoke(TargetService, args)!;
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
            {
                await OnCompletingInvocationAsync(method);
            }
        }

        public override async Task<T> InvokeAsyncT<T>(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            try
            {
                await OnStartingInvocationAsync(method);

                return await (Task<T>)method.Invoke(TargetService, args)!;
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
                await OnCompletingInvocationAsync(method);
            }
        }

        void EnsureTargetServiceNotNull()
        {
            if (TargetService is null)
                throw new InvalidOperationException("TargetService has not been set via SetTarget().");
        }

        protected abstract void OnStartingInvocation(MethodInfo targetMethod);
        protected abstract Task OnStartingInvocationAsync(MethodInfo targetMethod);
        protected abstract void OnCompletingInvocation(MethodInfo targetMethod);
        protected abstract Task OnCompletingInvocationAsync(MethodInfo targetMethod);
        protected abstract void OnInvocationException(MethodInfo targetMethod, Exception exception);
    }
}