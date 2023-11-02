using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public static class TentacleServiceDecoratorBuilderExtensions
    {
        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<TService>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics) where TService : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            return builder.RegisterInterceptor<TService>(new CallMetricsInterceptor(callMetrics).ToInterceptor());
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<T1, T2>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics)
            where T1 : class
            where T2 : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            var interceptor = new CallMetricsInterceptor(callMetrics).ToInterceptor();

            return builder
                .RegisterInterceptor<T1>(interceptor)
                .RegisterInterceptor<T2>(interceptor);
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<T1, T2, T3>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics)
            where T1 : class
            where T2 : class
            where T3 : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            var interceptor = new CallMetricsInterceptor(callMetrics).ToInterceptor();

            return builder
                .RegisterInterceptor<T1>(interceptor)
                .RegisterInterceptor<T2>(interceptor)
                .RegisterInterceptor<T3>(interceptor);
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService(this TentacleServiceDecoratorBuilder builder, Type serviceType, out CallMetrics callMetrics)
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            return builder.RegisterInterceptor(serviceType, new CallMetricsInterceptor(callMetrics).ToInterceptor());
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics, params Type[] serviceTypes)
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            var interceptor = new CallMetricsInterceptor(callMetrics).ToInterceptor();

            foreach (var serviceType in serviceTypes)
                builder.RegisterInterceptor(serviceType, interceptor);

            return builder;
        }

        public static TentacleServiceDecoratorBuilder RegisterInvocationHooks<TService>(this TentacleServiceDecoratorBuilder builder, Func<TService, Task>? preInvocation, string methodName)
            => RegisterInvocationHooks(builder, preInvocation, null, methodName);

        public static TentacleServiceDecoratorBuilder RegisterInvocationHooks<TService>(this TentacleServiceDecoratorBuilder builder, Func<TService, Task>? preInvocation, Func<TService, Task>? postInvocation, string? methodName = null)
            => builder.RegisterInterceptor<TService>(new InvocationHooksInterceptor<TService>(preInvocation, postInvocation, methodName).ToInterceptor());
    }
}