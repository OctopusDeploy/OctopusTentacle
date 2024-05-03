using System;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
{
    public static class TentacleServiceDecoratorBuilderExtensions
    {
        public static TentacleServiceDecoratorBuilder RecordMethodUsages<TService>(this TentacleServiceDecoratorBuilder builder, out IRecordedMethodUsages recordedUsages)
            where TService : class
        {
            var localMethodUsages = new MethodUsages();
            recordedUsages = localMethodUsages;

            return builder.RegisterProxyDecorator<TService>(service => MethodUsageProxyDecorator.Create(service, localMethodUsages));
        }

        public static TentacleServiceDecoratorBuilder HookServiceMethod<TService>(this TentacleServiceDecoratorBuilder builder, string methodName, PreMethodInvocationHook<TService, object> preInvocation) where TService : class
        //if we aren't hooking the post invocation, we don't care about the response type
            => HookServiceMethod<TService, object,  object>(builder, methodName, preInvocation, null);

        public static TentacleServiceDecoratorBuilder HookServiceMethod<TService, TRequest>(this TentacleServiceDecoratorBuilder builder, string methodName, PreMethodInvocationHook<TService, TRequest> preInvocation) where TService : class
        //if we aren't hooking the post invocation, we don't care about the response type
            => HookServiceMethod<TService, TRequest,  object>(builder, methodName, preInvocation, null);

        public static TentacleServiceDecoratorBuilder HookServiceMethod<TService, TRequest, TResponse>(this TentacleServiceDecoratorBuilder builder, string methodName, PreMethodInvocationHook<TService, TRequest>? preInvocation, PostMethodInvocationHook<TService, TResponse>? postInvocation) where TService : class
            => builder.RegisterProxyDecorator<TService>(service => MethodInvocationHookProxyDecorator<TService, TRequest, TResponse>.Create(service, methodName, preInvocation, postInvocation));
    }
}