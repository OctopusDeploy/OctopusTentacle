using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public delegate Task MethodInvocationHook<in TService>(TService service);
    public delegate Task MethodInvocationHook<in TService, in TResponse>(TService service, TResponse response);

    public class MethodInvocationHookProxyDecorator<TService, TResponse> : ServiceProxy where TService : class
    {
        MethodInvocationHook<TService>? preInvocation;
        MethodInvocationHook<TService, TResponse>? postInvocation;
        string methodName;

        void Configure(string methodName, MethodInvocationHook<TService>? preInvocation, MethodInvocationHook<TService, TResponse>? postInvocation)
        {
            this.preInvocation = preInvocation;
            this.postInvocation = postInvocation;
            this.methodName = methodName;
        }

        public static TService Create(TService targetService, string methodName, MethodInvocationHook<TService>? preInvocation, MethodInvocationHook<TService, TResponse>? postInvocation)
        {
            var proxiedService = DispatchProxyAsync.Create<TService, MethodInvocationHookProxyDecorator<TService, TResponse>>();
            var proxy = (proxiedService as MethodInvocationHookProxyDecorator<TService, TResponse>)!;
            proxy!.SetTargetService(targetService);
            proxy.Configure(methodName, preInvocation, postInvocation);

            return proxiedService;
        }

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            if (IsMethodThatIsToBeHooked(targetMethod))
            {
                preInvocation?.Invoke((TService)TargetService).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            if (preInvocation is not null && IsMethodThatIsToBeHooked(targetMethod))
            {
                await preInvocation((TService)TargetService);
            }
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod, object? response)
        {
            if (IsMethodThatIsToBeHooked(targetMethod))
            {
                postInvocation?.Invoke((TService)TargetService, (TResponse)response).GetAwaiter().GetResult();
            }
        }

        protected override async Task OnCompletingInvocationAsync(MethodInfo targetMethod, object? response)
        {
            if (postInvocation is not null && IsMethodThatIsToBeHooked(targetMethod))
            {
                await postInvocation((TService)TargetService, (TResponse)response);
            }
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
        }

        bool IsMethodThatIsToBeHooked(MemberInfo invocationMethod)
            => invocationMethod.Name.Equals(methodName);
    }
}