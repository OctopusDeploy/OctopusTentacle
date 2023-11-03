using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public abstract class ServiceProxy<TService> : DispatchProxyAsync
    {
        public TService Target { get; set; }

        public override object Invoke(MethodInfo method, object[] args)
        {
            try
            {
                OnStartingInvocation(method);

                return method.Invoke(Target, args)!;
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
            try
            {
                await OnStartingInvocationAsync(method);

                await (Task)method.Invoke(Target, args)!;
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
            try
            {
                await OnStartingInvocationAsync(method);

                return await (Task<T>)method.Invoke(Target, args)!;
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

        protected abstract void OnStartingInvocation(MethodInfo targetMethod);
        protected abstract Task OnStartingInvocationAsync(MethodInfo targetMethod);
        protected abstract void OnCompletingInvocation(MethodInfo targetMethod);
        protected abstract Task OnCompletingInvocationAsync(MethodInfo targetMethod);
        protected abstract void OnInvocationException(MethodInfo targetMethod, Exception exception);
    }
}