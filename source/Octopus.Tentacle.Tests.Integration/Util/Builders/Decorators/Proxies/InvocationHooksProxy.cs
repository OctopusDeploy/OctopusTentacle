using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class InvocationHooksProxy<TService> : ServiceProxy<TService>
    {
        Func<TService, Task>? preInvocation;
        Func<TService, Task>? postInvocation;
        string? methodName;

        internal void Configure(Func<TService, Task>? preInvocation, Func<TService, Task>? postInvocation, string? methodName)
        {
            this.preInvocation = preInvocation;
            this.postInvocation = postInvocation;
            this.methodName = methodName;
        }

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            if (IsMethodInteresting(targetMethod))
            {
                preInvocation?.Invoke(Target).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            if (preInvocation is not null && IsMethodInteresting(targetMethod))
            {
                await preInvocation(Target);
            }
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod)
        {
            if (IsMethodInteresting(targetMethod))
            {
                postInvocation?.Invoke(Target).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnCompletingInvocationAsync(MethodInfo targetMethod)
        {
            if (postInvocation is not null && IsMethodInteresting(targetMethod))
            {
                await postInvocation(Target);
            }
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
        }

        bool IsMethodInteresting(MemberInfo invocationMethod)
            => invocationMethod.Name.Equals(methodName);
    }
}