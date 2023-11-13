using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class MethodUsageProxyDecorator : ServiceProxy
    {
        MethodUsages usages;

        void Configure(MethodUsages usages)
        {
            this.usages = usages;
        }

        public static TService Create<TService>(TService targetService, IRecordedMethodUsages usages) where TService : class
        {
            var proxiedService = DispatchProxyAsync.Create<TService, MethodUsageProxyDecorator>();
            var proxy = (proxiedService as MethodUsageProxyDecorator)!;
            proxy!.SetTargetService(targetService);
            proxy!.Configure((MethodUsages)usages);

            return proxiedService;
        }

        public static object Create(Type scriptServiceType, object targetService, IRecordedMethodUsages usages)
        {
            var genericCreateMethod = typeof(DispatchProxyAsync).GetMethod(nameof(DispatchProxyAsync.Create), BindingFlags.Public | BindingFlags.Static);
            var concreteMethodInfo = genericCreateMethod!.MakeGenericMethod(scriptServiceType, typeof(MethodUsageProxyDecorator));

            var proxiedService = concreteMethodInfo.Invoke(null, null);
            var proxy = (proxiedService as MethodUsageProxyDecorator)!;
            proxy!.SetTargetService(targetService);
            proxy!.Configure((MethodUsages)usages);

            return proxiedService!;
        }

        protected override Task OnStartingInvocationAsync(MethodInfo targetMethod, object? request)
        {
            usages.RecordCallStart(targetMethod);
            return Task.CompletedTask;
        }

        protected override Task OnCompletingInvocationAsync(MethodInfo targetMethod, object? response)
        {
            usages.RecordCallComplete(targetMethod);
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
            usages.RecordCallException(targetMethod, exception);
        }
    }
}