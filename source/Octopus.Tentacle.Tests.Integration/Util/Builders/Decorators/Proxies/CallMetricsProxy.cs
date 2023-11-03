using System;
using System.Reflection;
using System.Threading.Tasks;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class CallMetricsProxy<TService> : ServiceProxy<TService>
    {
         IRecordableCallMetrics callMetrics;

        internal void Configure(IRecordableCallMetrics callMetrics)
        {
            this.callMetrics = callMetrics;
        }

        protected override void OnStartingInvocation(MethodInfo targetMethod)
        {
            callMetrics.RecordCallStart(targetMethod);
        }

        protected override Task OnStartingInvocationAsync(MethodInfo targetMethod)
        {
            callMetrics.RecordCallStart(targetMethod);
            return Task.CompletedTask;
        }

        protected override void OnCompletingInvocation(MethodInfo targetMethod)
        {
            callMetrics.RecordCallComplete(targetMethod);
        }

        protected override Task OnCompletingInvocationAsync(MethodInfo targetMethod)
        {
            callMetrics.RecordCallComplete(targetMethod);
            return Task.CompletedTask;
        }

        protected override void OnInvocationException(MethodInfo targetMethod, Exception exception)
        {
            callMetrics.RecordCallException(targetMethod, exception);
        }
    }
}