using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceCallCounts
    {
        public long StartScriptCallCountStarted;
        public long GetStatusCallCountStarted;
        public long CancelScriptCallCountStarted;
        public long CompleteScriptCallCountStarted;

        public long StartScriptCallCountComplete;
    }

    public class CountingCallsScriptServiceDecorator : IAsyncClientScriptService
    {
        private readonly ScriptServiceCallCounts scriptServiceCallCounts;
        private readonly IAsyncClientScriptService inner;

        public CountingCallsScriptServiceDecorator(IAsyncClientScriptService inner, ScriptServiceCallCounts scriptServiceCallCounts)
        {
            this.inner = inner;
            this.scriptServiceCallCounts = scriptServiceCallCounts;
        }

        public async Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.StartScriptCallCountStarted);
            try
            {
                return await inner.StartScriptAsync(command, options);
            }
            finally
            {
                Interlocked.Increment(ref scriptServiceCallCounts.StartScriptCallCountComplete);
            }
        }

        public async Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.GetStatusCallCountStarted);
            return await inner.GetStatusAsync(request, options);
        }

        public async Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.CancelScriptCallCountStarted);
            return await inner.CancelScriptAsync(command, options);
        }

        public async Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.CompleteScriptCallCountStarted);
            return await inner.CompleteScriptAsync(command, options);
        }
    }

    public static class TentacleServiceDecoratorBuilderCountingCallsScriptServiceDecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder CountCallsToScriptService(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out ScriptServiceCallCounts scriptServiceCallCounts)
        {
            var myScriptServiceCallCounts = new ScriptServiceCallCounts();
            scriptServiceCallCounts = myScriptServiceCallCounts;
            return tentacleServiceDecoratorBuilder.DecorateScriptServiceWith(inner => new CountingCallsScriptServiceDecorator(inner, myScriptServiceCallCounts));
        }
    }
}