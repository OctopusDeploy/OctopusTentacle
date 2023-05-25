using System.Threading;
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
    }
    
    public class CountingCallsScriptServiceV2Decorator : IScriptServiceV2
    {
        private ScriptServiceV2CallCounts counts;
        

        private IScriptServiceV2 inner;

        public CountingCallsScriptServiceV2Decorator(IScriptServiceV2 inner, ScriptServiceV2CallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
        {
            Interlocked.Increment(ref counts.StartScriptCallCountStarted);
            try
            {
                return inner.StartScript(command);
            }
            finally
            {
                Interlocked.Increment(ref counts.StartScriptCallCountComplete);
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            Interlocked.Increment(ref counts.GetStatusCallCountStarted);
            return inner.GetStatus(request);
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            Interlocked.Increment(ref counts.CancelScriptCallCountStarted);
            return inner.CancelScript(command);
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            Interlocked.Increment(ref counts.CompleteScriptCallCountStarted);
            inner.CompleteScript(command);
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