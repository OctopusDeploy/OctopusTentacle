using System;
using System.Reflection;
using System.Threading.Tasks;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public static class TentacleServiceDecoratorBuilderExtensions
    {
        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<TService>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics) where TService : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;

            return builder.RegisterProxyBuilder<TService>(svc =>
            {
                var proxy = DispatchProxyAsync.Create<TService, CallMetricsProxy<TService>>() as CallMetricsProxy<TService>;
                proxy.Target = svc;
                proxy.Configure(localCallMetrics);

                return proxy as TService;
            });
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<T1, T2>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics)
            where T1 : class
            where T2 : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;

            return builder.RegisterProxyBuilder<T1>(svc =>
                {
                    var proxy = DispatchProxyAsync.Create<T1, CallMetricsProxy<T1>>() as CallMetricsProxy<T1>;
                    proxy.Target = svc;
                    proxy.Configure(localCallMetrics);

                    return proxy as T1;
                }).RegisterProxyBuilder<T2>(svc =>
                {
                    var proxy = DispatchProxyAsync.Create<T2, CallMetricsProxy<T2>>() as CallMetricsProxy<T2>;
                    proxy.Target = svc;
                    proxy.Configure(localCallMetrics);

                    return proxy as T2;
                });
        }


        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<T1, T2, T3>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics)
            where T1 : class
            where T2 : class
            where T3 : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;

            return builder.RegisterProxyBuilder<T1>(svc =>
            {
                var proxy = DispatchProxyAsync.Create<T1, CallMetricsProxy<T1>>() as CallMetricsProxy<T1>;
                proxy.Target = svc;
                proxy.Configure(localCallMetrics);

                return proxy as T1;
            }).RegisterProxyBuilder<T2>(svc =>
            {
                var proxy = DispatchProxyAsync.Create<T2, CallMetricsProxy<T2>>() as CallMetricsProxy<T2>;
                proxy.Target = svc;
                proxy.Configure(localCallMetrics);

                return proxy as T2;
            }).RegisterProxyBuilder<T3>(svc =>
            {
                var proxy = DispatchProxyAsync.Create<T3, CallMetricsProxy<T3>>() as CallMetricsProxy<T3>;
                proxy.Target = svc;
                proxy.Configure(localCallMetrics);

                return proxy as T3;
            });
        }

        public static TentacleServiceDecoratorBuilder RegisterInvocationHooks<TService>(this TentacleServiceDecoratorBuilder builder, Func<TService, Task>? preInvocation, string methodName) where TService : class
            => RegisterInvocationHooks(builder, preInvocation, null, methodName);

        public static TentacleServiceDecoratorBuilder RegisterInvocationHooks<TService>(this TentacleServiceDecoratorBuilder builder, Func<TService, Task>? preInvocation, Func<TService, Task>? postInvocation, string? methodName = null) where TService : class
        {
            return builder.RegisterProxyBuilder<TService>(svc =>
            {
                var proxy = DispatchProxyAsync.Create<TService, InvocationHooksProxy<TService>>() as InvocationHooksProxy<TService>;
                proxy.Target = svc;
                proxy.Configure(preInvocation, postInvocation, methodName);

                return proxy as TService;
            });
        }
    }
}