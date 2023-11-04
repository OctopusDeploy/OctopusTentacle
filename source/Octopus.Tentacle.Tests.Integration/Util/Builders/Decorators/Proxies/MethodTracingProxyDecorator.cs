using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class MethodTracingProxyDecorator : ServiceProxy
    {
        MethodTracingStats tracingStats;

        void Configure(MethodTracingStats tracingStats)
        {
            this.tracingStats = tracingStats;
        }

        public static TService Create<TService>(TService targetService, out IRecordedMethodTracingStats tracingStats) where TService : class
        {
            var localTracingStats = new MethodTracingStats();
            tracingStats = localTracingStats;

            var proxiedService = DispatchProxyAsync.Create<TService, MethodTracingProxyDecorator>();
            var proxy = (proxiedService as MethodTracingProxyDecorator)!;
            proxy!.SetTargetService(targetService);
            proxy.Configure(localTracingStats);

            return proxiedService;
        }

        public static TService Create<TService>(TService targetService, IRecordedMethodTracingStats tracingStats) where TService : class
        {
            var proxiedService = DispatchProxyAsync.Create<TService, MethodTracingProxyDecorator>();
            var proxy = (proxiedService as MethodTracingProxyDecorator)!;
            proxy!.SetTargetService(targetService);
            proxy!.Configure((MethodTracingStats)tracingStats);

            return proxiedService;
        }

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            tracingStats.RecordCallStart(targetMethod);
        }

        protected override Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            tracingStats.RecordCallStart(targetMethod);
            return Task.CompletedTask;
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod)
        {
            tracingStats.RecordCallComplete(targetMethod);
        }

        protected override Task OnCompletingInvocationAsync(MethodInfo targetMethod)
        {
            tracingStats.RecordCallComplete(targetMethod);
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
            tracingStats.RecordCallException(targetMethod, exception);
        }
    }
}