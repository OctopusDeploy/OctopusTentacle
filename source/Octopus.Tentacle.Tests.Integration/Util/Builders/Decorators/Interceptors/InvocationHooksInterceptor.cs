using System;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public class InvocationHooksInterceptor<TService> : AsyncInterceptor
    {
        readonly Func<TService, Task>? preInvocation;
        readonly Func<TService, Task>? postInvocation;
        readonly string? methodName;

        public InvocationHooksInterceptor(Func<TService, Task>? preInvocation, Func<TService, Task>? postInvocation, string? methodName)
        {
            this.preInvocation = preInvocation;
            this.postInvocation = postInvocation;
            this.methodName = methodName;
        }

        protected override void OnStartingInvocation(IInvocation invocation)
        {
            if (IsMethodInteresting(invocation.Method))
            {
                preInvocation?.Invoke((TService)invocation.InvocationTarget).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnStartingInvocationAsync(IInvocation invocation)
        {
            if (preInvocation is not null && IsMethodInteresting(invocation.Method))
            {
                await preInvocation((TService)invocation.InvocationTarget);
            }
        }

        protected override void OnCompletingInvocation(IInvocation invocation)
        {
            if (IsMethodInteresting(invocation.Method))
            {
                postInvocation?.Invoke((TService)invocation.InvocationTarget).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnCompletingInvocationAsync(IInvocation invocation)
        {
            if (postInvocation is not null && IsMethodInteresting(invocation.Method))
            {
                await postInvocation((TService)invocation.InvocationTarget);
            }
        }

        protected override void OnInvocationException(IInvocation invocation, Exception exception)
        {
        }

        bool IsMethodInteresting(MemberInfo invocationMethod)
            => invocationMethod.Name.Equals(methodName);

        // protected override object StartingInvocation(IInvocation invocation)
        // {
        //     if (IsMethodInteresting(invocation.Method))
        //     {
        //         preInvocation?.Invoke((TService)invocation.InvocationTarget).GetAwaiter().GetResult();
        //     }
        //
        //     return new object();
        // }
        //
        // protected override void CompletedInvocation(IInvocation invocation, object state)
        // {
        //     if (IsMethodInteresting(invocation.Method))
        //     {
        //         postInvocation?.Invoke((TService)invocation.InvocationTarget).GetAwaiter().GetResult();
        //     }
        // }
    }
}