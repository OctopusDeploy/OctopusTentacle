using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies
{
    public class MethodUsages : IRecordedMethodUsages
    {
        readonly ConcurrentDictionary<string, Lazy<MethodUsage>> trackedMethods = new();

        IRecordedMethodUsage IRecordedMethodUsages.For(string methodName) => GetMethodStats(methodName);

        public IRecordedMethodUsage ForAll()
        {
            return trackedMethods.Aggregate(new MethodUsage(), (usage, kvp) =>
            {
                usage.Add(kvp.Value.Value);
                return usage;
            });
        }

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

        public void RecordCallException(MethodInfo targetMethod, Exception exception)
        {
            var stats = GetMethodStats(targetMethod);
            stats.RecordException(exception);
        }

        MethodUsage GetMethodStats(string methodName) => trackedMethods.GetOrAdd(methodName, _ => new Lazy<MethodUsage>(() => new MethodUsage())).Value;
        MethodUsage GetMethodStats(MethodInfo targetMethod) => GetMethodStats(targetMethod.Name);
    }

    public interface IRecordedMethodUsages
    {
        IRecordedMethodUsage For(string methodName);
        IRecordedMethodUsage ForAll();
    }

    public class MethodUsage : IRecordedMethodUsage
    {
        long started;
        long completed;

        public long Started => started;
        public long Completed => completed;
        public Exception? LastException { get; private set; }

        public void RecordStarted() => Interlocked.Increment(ref started);

        public void RecordCompleted() => Interlocked.Increment(ref completed);

        public void RecordException(Exception exception) => this.LastException = exception;

        public void Add(MethodUsage methodUsage)
        {
            started += methodUsage.Started;
            completed += methodUsage.Completed;

            // if the incoming last exception is not null, overwrite
            if (methodUsage.LastException is not null)
            {
                LastException = methodUsage.LastException;
            }
        }
    }

    public interface IRecordedMethodUsage
    {
        long Started { get; }
        long Completed { get; }
        Exception? LastException { get; }
    }
}