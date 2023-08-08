using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV2CallCounts
    {
        public long StartScriptCallCountStarted;
        public long GetStatusCallCountStarted;
        public long CancelScriptCallCountStarted;
        public long CompleteScriptCallCountStarted;

        public long StartScriptCallCountComplete;
        public long GetStatusCallCountCompleted;
        public long CancelScriptCallCountCompleted;
        public long CompleteScriptCallCountCompleted;
    }

    public class CountingCallsScriptServiceV2Decorator : IAsyncClientScriptServiceV2
    {
        private ScriptServiceV2CallCounts counts;


        private IAsyncClientScriptServiceV2 inner;

        public CountingCallsScriptServiceV2Decorator(IAsyncClientScriptServiceV2 inner, ScriptServiceV2CallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.StartScriptCallCountStarted);
            try
            {
                return await inner.StartScriptAsync(command, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.StartScriptCallCountComplete);
            }
        }

        public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.GetStatusCallCountStarted);
            try
            {
                return await inner.GetStatusAsync(request, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.GetStatusCallCountCompleted);
            }
        }

        public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.CancelScriptCallCountStarted);
            try
            {
                return await inner.CancelScriptAsync(command, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.CancelScriptCallCountCompleted);
            }
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.CompleteScriptCallCountStarted);
            try
            {
                await inner.CompleteScriptAsync(command, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.CompleteScriptCallCountCompleted);
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderCountingCallsScriptServiceV2DecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder CountCallsToScriptServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out ScriptServiceV2CallCounts scriptServiceV2CallCounts)
        {
            var myScriptServiceV2CallCounts = new ScriptServiceV2CallCounts();
            scriptServiceV2CallCounts = myScriptServiceV2CallCounts;
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceV2With(inner => new CountingCallsScriptServiceV2Decorator(inner, myScriptServiceV2CallCounts));
        }
    }
}