using System.Threading;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
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

    public class CountingCallsScriptServiceV2Decorator : IClientScriptServiceV2
    {
        private ScriptServiceV2CallCounts counts;


        private IClientScriptServiceV2 inner;

        public CountingCallsScriptServiceV2Decorator(IClientScriptServiceV2 inner, ScriptServiceV2CallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.StartScriptCallCountStarted);
            try
            {
                return inner.StartScript(command, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.StartScriptCallCountComplete);
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.GetStatusCallCountStarted);
            try
            {
                return inner.GetStatus(request, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.GetStatusCallCountCompleted);
            }
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.CancelScriptCallCountStarted);
            try
            {
                return inner.CancelScript(command, options);
            }
            finally
            {
                Interlocked.Increment(ref counts.CancelScriptCallCountCompleted);
            }
        }

        public void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.CompleteScriptCallCountStarted);
            try
            {
                inner.CompleteScript(command, options);
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