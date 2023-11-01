using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors
{
    public class CallMetricsInterceptor : IAsyncInterceptor
    {
        readonly IRecordableCallMetrics callMetrics;

        public CallMetricsInterceptor(IRecordableCallMetrics callMetrics)
        {
            this.callMetrics = callMetrics;
        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            try
            {
                callMetrics.RecordCallStart(invocation.Method);
                invocation.Proceed();
            }
            catch (Exception e)
            {
                callMetrics.RecordCallException(invocation.Method, e);
                throw;
            }
            finally
            {
                callMetrics.RecordCallComplete(invocation.Method);
            }
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
        }

        async Task InternalInterceptAsynchronous(IInvocation invocation)
        {
            try
            {
                callMetrics.RecordCallStart(invocation.Method);

                invocation.Proceed();

                await (Task)invocation.ReturnValue;
            }
            catch (Exception e)
            {
                callMetrics.RecordCallException(invocation.Method, e);
                throw;
            }
            finally
            {
                callMetrics.RecordCallComplete(invocation.Method);
            }
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(invocation);
        }

        async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
        {
            try
            {
                callMetrics.RecordCallStart(invocation.Method);

                invocation.Proceed();

                return await (Task<TResult>)invocation.ReturnValue;
            }
            catch (Exception e)
            {
                callMetrics.RecordCallException(invocation.Method, e);
                throw;
            }
            finally
            {
                callMetrics.RecordCallComplete(invocation.Method);
            }
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