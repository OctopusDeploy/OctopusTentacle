using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV3AlphaCallCounts
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

    public class CountingCallsScriptServiceV3AlphaDecorator : IAsyncClientScriptServiceV3Alpha
    {
        private ScriptServiceV3AlphaCallCounts counts;


        private IAsyncClientScriptServiceV3Alpha inner;

        public CountingCallsScriptServiceV3AlphaDecorator(IAsyncClientScriptServiceV3Alpha inner, ScriptServiceV3AlphaCallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public async Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, HalibutProxyRequestOptions options)
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

        public async Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, HalibutProxyRequestOptions options)
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

        public async Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, HalibutProxyRequestOptions options)
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

        public async Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, HalibutProxyRequestOptions options)
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

    public static class TentacleServiceDecoratorBuilderCountingCallsScriptServiceV3AlphaDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder CountCallsToScriptServiceV3Alpha(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out ScriptServiceV3AlphaCallCounts scriptServiceV3AlphaCallCounts)
        {
            var myScriptServiceV3AlphaCallCounts = new ScriptServiceV3AlphaCallCounts();
            scriptServiceV3AlphaCallCounts = myScriptServiceV3AlphaCallCounts;
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceV3AlphaWith(inner => new CountingCallsScriptServiceV3AlphaDecorator(inner, myScriptServiceV3AlphaCallCounts));
        }
    }
}