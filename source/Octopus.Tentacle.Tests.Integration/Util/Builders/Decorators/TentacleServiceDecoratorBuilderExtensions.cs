using System;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
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

        public static TentacleServiceDecoratorBuilder RecordMethodUsages(this TentacleServiceDecoratorBuilder builder, TentacleConfigurationTestCase testCase, out IRecordedMethodUsages recordedUsages)
        {
            var localMethodUsages = new MethodUsages();
            recordedUsages = localMethodUsages;

            return builder.RegisterProxyDecorator(testCase.LatestScriptServiceType, service => MethodUsageProxyDecorator.Create(testCase.LatestScriptServiceType, service, localMethodUsages));
        }

        public static TentacleServiceDecoratorBuilder HookServiceMethod<TService>(this TentacleServiceDecoratorBuilder builder, string methodName, PreMethodInvocationHook<TService> preInvocation) where TService : class
        //if we aren't hooking the post invocation, we don't care about the response type
            => HookServiceMethod<TService, object>(builder, methodName, preInvocation, null);

        public static TentacleServiceDecoratorBuilder HookServiceMethod<TService, TResponse>(this TentacleServiceDecoratorBuilder builder, string methodName, PreMethodInvocationHook<TService>? preInvocation, PostMethodInvocationHook<TService, TResponse>? postInvocation) where TService : class
            => builder.RegisterProxyDecorator<TService>(service => MethodInvocationHookProxyDecorator<TService, TResponse>.Create(service, methodName, preInvocation, postInvocation));

        public static TentacleServiceDecoratorBuilder HookServiceMethod(this TentacleServiceDecoratorBuilder builder, TentacleConfigurationTestCase testCase, string methodName, PreMethodInvocationHook<object> preInvocation)
        {
            var proxyType = typeof(MethodInvocationHookProxyDecorator<,,>);
            var concreteType = proxyType.MakeGenericType(testCase.LatestScriptServiceType, typeof(object));

            var createMethodInfo = concreteType.GetMethod(nameof(MethodInvocationHookProxyDecorator<object, object>.Create));

            return builder.RegisterProxyDecorator(testCase.LatestScriptServiceType, service =>
            {
                return createMethodInfo.Invoke(null, new object?[] { service, methodName, preInvocation, null });
            });
        }
    }
}