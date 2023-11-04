using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public delegate Task MethodInvocationHook<in TService>(TService service);

    public class MethodInvocationHookProxyDecorator<TService> : ServiceProxy where TService : class
    {
        MethodInvocationHook<TService>? preInvocation;
        MethodInvocationHook<TService>? postInvocation;
        string methodName;

        void Configure(string methodName, MethodInvocationHook<TService>? preInvocation, MethodInvocationHook<TService>? postInvocation)
        {
            this.preInvocation = preInvocation;
            this.postInvocation = postInvocation;
            this.methodName = methodName;
        }

        public static TService Create(TService targetService, string methodName, MethodInvocationHook<TService>? preInvocation, MethodInvocationHook<TService>? postInvocation)
        {
            var proxiedService = DispatchProxyAsync.Create<TService, MethodInvocationHookProxyDecorator<TService>>();
            var proxy = (proxiedService as MethodInvocationHookProxyDecorator<TService>)!;
            proxy!.SetTargetService(targetService);
            proxy.Configure(methodName, preInvocation, postInvocation);

            return proxiedService;
        }

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            if (IsMethodInteresting(targetMethod))
            {
                preInvocation?.Invoke((TService)TargetService).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            if (preInvocation is not null && IsMethodInteresting(targetMethod))
            {
                await preInvocation((TService)TargetService);
            }
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod)
        {
            if (IsMethodInteresting(targetMethod))
            {
                postInvocation?.Invoke((TService)TargetService).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnCompletingInvocationAsync(MethodInfo targetMethod)
        {
            if (postInvocation is not null && IsMethodInteresting(targetMethod))
            {
                await postInvocation((TService)TargetService);
            }
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
        }

        bool IsMethodInteresting(MemberInfo invocationMethod)
            => invocationMethod.Name.Equals(methodName);
    }
}