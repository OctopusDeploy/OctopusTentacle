using System.Threading;
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
    public class CountingCallsScriptServiceDecorator : IScriptService
    {
        private ScriptServiceCallCounts scriptServiceCallCounts;
        private IScriptService inner;

        public CountingCallsScriptServiceDecorator(IScriptService inner, ScriptServiceCallCounts scriptServiceCallCounts)
        {
            this.inner = inner;
            this.scriptServiceCallCounts = scriptServiceCallCounts;
        }

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.StartScriptCallCountStarted);
            try
            {
                return inner.StartScript(command);
            }
            finally
            {
                Interlocked.Increment(ref scriptServiceCallCounts.StartScriptCallCountComplete);
            }
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.GetStatusCallCountStarted);
            return inner.GetStatus(request);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.CancelScriptCallCountStarted);
            return inner.CancelScript(command);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            Interlocked.Increment(ref scriptServiceCallCounts.CompleteScriptCallCountStarted);
            return inner.CompleteScript(command);
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