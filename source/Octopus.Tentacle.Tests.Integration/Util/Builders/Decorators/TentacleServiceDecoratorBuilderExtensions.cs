using System.Collections.Immutable;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public static class TentacleServiceDecoratorBuilderExtensions
    {
        public static TentacleServiceDecoratorBuilder TraceMethods<TService>(this TentacleServiceDecoratorBuilder builder, out IRecordedMethodTracingStats recordedTracingStats)
            where TService : class
        {
            var localTracingStats = new MethodTracingStats();
            recordedTracingStats = localTracingStats;

            return builder.RegisterProxyDecorator<TService>(service => MethodTracingProxyDecorator<TService>.Create(service, localTracingStats));
        }

        public static TentacleServiceDecoratorBuilder TraceMethods<TSyncService, TAsyncService>(this TentacleServiceDecoratorBuilder builder, out IRecordedMethodTracingStats recordedTracingStats)
            where TSyncService : class
            where TAsyncService : class
        {
            var localTracingStats = new MethodTracingStats();
            recordedTracingStats = localTracingStats;

            return builder
                .RegisterProxyDecorator<TSyncService>(service => MethodTracingProxyDecorator<TSyncService>.Create(service, localTracingStats))
                .RegisterProxyDecorator<TAsyncService>(service => MethodTracingProxyDecorator<TAsyncService>.Create(service, localTracingStats));

        }

        public static TentacleServiceDecoratorBuilder HookMethodInvocation<TService>(this TentacleServiceDecoratorBuilder builder, string methodName, MethodInvocationHook<TService>? preInvocation) where TService : class
            => HookMethodInvocation(builder, methodName, preInvocation, null);

        public static TentacleServiceDecoratorBuilder HookMethodInvocation<TService>(this TentacleServiceDecoratorBuilder builder, string methodName, MethodInvocationHook<TService>? preInvocation, MethodInvocationHook<TService>? postInvocation) where TService : class
        {
            return builder.RegisterProxyDecorator<TService>(service => MethodInvocationHookProxyDecorator<TService>.Create(service, methodName, preInvocation, postInvocation));
        }
    }
}