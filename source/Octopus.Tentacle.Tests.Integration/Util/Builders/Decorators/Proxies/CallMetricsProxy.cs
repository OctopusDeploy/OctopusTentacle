using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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
    public interface IRecordableCallMetrics
    {
        void RecordCallStart(MethodInfo method);
        void RecordCallComplete(MethodInfo method);
        void RecordCallException(MethodInfo method, Exception exception);
    }

    public class CallMetrics : IRecordableCallMetrics
    {
        readonly ConcurrentDictionary<string, long> callStarts = new();
        readonly ConcurrentDictionary<string, long> callCompletions = new();
        readonly ConcurrentDictionary<string, Exception> latestCallExceptions = new();

        void IRecordableCallMetrics.RecordCallStart(MethodInfo method)
            => callStarts.AddOrUpdate(NormalizeMethodName(method), 1, (k, v) => Interlocked.Increment(ref v));

        void IRecordableCallMetrics.RecordCallComplete(MethodInfo method)
            => callCompletions.AddOrUpdate(NormalizeMethodName(method), 1, (k, v) => Interlocked.Increment(ref v));

        void IRecordableCallMetrics.RecordCallException(MethodInfo method, Exception exception)
            => latestCallExceptions.AddOrUpdate(NormalizeMethodName(method), exception, (_, _) => exception);

        public long StartedCount(string methodName)
            => callStarts.TryGetValue(NormalizeMethodName(methodName), out var count) ? count : 0L;
        public long CompletedCount(string methodName)
            => callCompletions.TryGetValue(NormalizeMethodName(methodName), out var count) ? count : 0L;

        public Exception? LatestException(string methodName)
            => latestCallExceptions.TryGetValue(NormalizeMethodName(methodName), out var exception) ? exception : null;

        static string NormalizeMethodName(string methodName)
            //we normalize to include the async suffix as all methods will be async in the future
            => !methodName.EndsWith("Async")
                ? $"{methodName}Async"
                : methodName;

        static string NormalizeMethodName(MethodInfo method)
        {
            return NormalizeMethodName(method.Name);
        }
    }
}