using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public delegate Task PreMethodInvocationHook<in TService, in TRequest>(TService service, TRequest request);
    public delegate Task PostMethodInvocationHook<in TService, in TResponse>(TService service, TResponse response);

    public class MethodInvocationHookProxyDecorator<TService,TRequest, TResponse> : ServiceProxy where TService : class
    {
        PreMethodInvocationHook<TService, TRequest>? preInvocation;
        PostMethodInvocationHook<TService, TResponse>? postInvocation;
        string methodName;

        void Configure(string methodName, PreMethodInvocationHook<TService, TRequest>? preInvocation, PostMethodInvocationHook<TService, TResponse>? postInvocation)
        {
            this.preInvocation = preInvocation;
            this.postInvocation = postInvocation;
            this.methodName = methodName;
        }

        public static TService Create(TService targetService, string methodName, PreMethodInvocationHook<TService, TRequest>? preInvocation, PostMethodInvocationHook<TService, TResponse>? postInvocation)
        {
            var proxiedService = DispatchProxyAsync.Create<TService, MethodInvocationHookProxyDecorator<TService, TRequest, TResponse>>();
            var proxy = (proxiedService as MethodInvocationHookProxyDecorator<TService, TRequest, TResponse>)!;
            proxy!.SetTargetService(targetService);
            proxy.Configure(methodName, preInvocation, postInvocation);

            return proxiedService;
        }

        protected override async Task OnStartingInvocationAsync(MethodInfo targetMethod, object? request)
        {
            if (preInvocation is not null && IsMethodThatIsToBeHooked(targetMethod))
            {
                await preInvocation((TService)TargetService, (TRequest)request);
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