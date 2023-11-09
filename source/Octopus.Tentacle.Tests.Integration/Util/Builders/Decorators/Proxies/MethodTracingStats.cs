using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class MethodTracingStats : IRecordedMethodTracingStats
    {
        readonly ConcurrentDictionary<string, Lazy<TracedMethodStats>> trackedMethods = new();

        IRecordedTracedMethodStats IRecordedMethodTracingStats.For(string methodName) => GetMethodStats(methodName);

        public void RecordCallStart(MethodInfo targetMethod)
        {
            var stats = GetMethodStats(targetMethod);
            stats.RecordStarted();
        }

        public void RecordCallComplete(MethodInfo targetMethod)
        {
            var stats = GetMethodStats(targetMethod);
            stats.RecordCompleted();
        }

        public  void RecordCallException(MethodInfo targetMethod, Exception exception)
        {
            var stats = GetMethodStats(targetMethod);
            stats.RecordException(exception);
        }

        TracedMethodStats GetMethodStats(string methodName) => trackedMethods.GetOrAdd(methodName, _ => new Lazy<TracedMethodStats>(() => new TracedMethodStats())).Value;
        TracedMethodStats GetMethodStats(MethodInfo targetMethod) => GetMethodStats(targetMethod.Name);
    }

    public interface IRecordedMethodTracingStats
    {
        IRecordedTracedMethodStats For(string methodName);
    }

    public class TracedMethodStats : IRecordedTracedMethodStats
    {
        long started;
        long completed;

        public long Started => started;
        public long Completed => completed;
        public Exception? LastException { get; set; }

        public void RecordStarted() => Interlocked.Increment(ref started);

        public void RecordCompleted() => Interlocked.Increment(ref completed);

        public void RecordException(Exception exception) => LastException = exception;
    }

    public interface IRecordedTracedMethodStats
    {
        long Started { get; }
        long Completed { get; }
        Exception? LastException { get; set; }
    }
}