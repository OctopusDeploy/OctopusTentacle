using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public class InvocationHooksInterceptor<TService> : AsyncInterceptor
    {
        readonly Func<TService, Task>? preInvocation;
        readonly Func<TService, Task>? postInvocation;

        public InvocationHooksInterceptor(Func<TService, Task>? preInvocation, Func<TService, Task>? postInvocation)
        {
            this.preInvocation = preInvocation;
            this.postInvocation = postInvocation;
        }

        protected override async Task OnStartingInvocationAsync(IInvocation invocation)
        {
            if (preInvocation is not null)
                await preInvocation((TService)invocation.InvocationTarget);
        }

        protected override async Task OnCompletingInvocationAsync(IInvocation invocation)
        {
            if (postInvocation is not null)
                await postInvocation((TService)invocation.InvocationTarget);
        }
    }
}