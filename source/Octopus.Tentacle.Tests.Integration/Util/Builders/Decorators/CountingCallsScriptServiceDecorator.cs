using System;
using System.Threading;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;

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

    public class CountingCallsScriptServiceDecorator : IClientScriptService
    {
        private readonly ScriptServiceCallCounts scriptServiceCallCounts;
        private readonly IClientScriptService inner;

        public CountingCallsScriptServiceDecorator(IClientScriptService inner, ScriptServiceCallCounts scriptServiceCallCounts)
        {
            this.inner = inner;
            this.scriptServiceCallCounts = scriptServiceCallCounts;
        }

        public ScriptTicket StartScript(StartScriptCommand command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.StartScriptCallCountStarted);
            try
            {
                return inner.StartScript(command, options);
            }
            finally
            {
                Interlocked.Increment(ref scriptServiceCallCounts.StartScriptCallCountComplete);
            }
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.GetStatusCallCountStarted);
            return inner.GetStatus(request, options);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.CancelScriptCallCountStarted);
            return inner.CancelScript(command, options);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command, HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.CompleteScriptCallCountStarted);
            return inner.CompleteScript(command, options);
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