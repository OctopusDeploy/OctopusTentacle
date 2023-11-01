using System;
using Castle.DynamicProxy;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public static class TentacleServiceDecoratorBuilderExtensions
    {
        public static TentacleServiceDecoratorBuilder LogCallsToService<TService>(this TentacleServiceDecoratorBuilder builder) where TService : class
        {
            return builder.RegisterInterceptor<TService>(new CallLoggingInterceptor().ToInterceptor());
        }

        public static TentacleServiceDecoratorBuilder LogCallsToService<TSyncService, TAsyncService>(this TentacleServiceDecoratorBuilder builder)
            where TSyncService : class
            where TAsyncService : class
        {
            var interceptor = new CallLoggingInterceptor().ToInterceptor();
            return builder
                .RegisterInterceptor<TSyncService>(interceptor)
                .RegisterInterceptor<TAsyncService>(interceptor);
        }

        public static TentacleServiceDecoratorBuilder LogCallsToService(this TentacleServiceDecoratorBuilder builder, Type serviceType)
        {
            return builder.RegisterInterceptor(serviceType, new CallLoggingInterceptor().ToInterceptor());
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<TService>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics) where TService : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            return builder.RegisterInterceptor<TService>(new CallMetricsInterceptor(callMetrics).ToInterceptor());
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService<TSyncService, TAsyncService>(this TentacleServiceDecoratorBuilder builder, out CallMetrics callMetrics)
            where TSyncService : class
            where TAsyncService : class
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            var interceptor = new CallMetricsInterceptor(callMetrics).ToInterceptor();

            return builder
                .RegisterInterceptor<TSyncService>(interceptor)
                .RegisterInterceptor<TAsyncService>(interceptor);
        }

        public static TentacleServiceDecoratorBuilder RecordCallMetricsToService(this TentacleServiceDecoratorBuilder builder, Type serviceType, out CallMetrics callMetrics)
        {
            var localCallMetrics = new CallMetrics();
            callMetrics = localCallMetrics;
            return builder.RegisterInterceptor(serviceType, new CallMetricsInterceptor(callMetrics).ToInterceptor());
        }
    }
}