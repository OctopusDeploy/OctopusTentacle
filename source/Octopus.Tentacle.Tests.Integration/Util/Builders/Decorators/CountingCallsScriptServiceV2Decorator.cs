using System.Threading;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CountingCallsScriptServiceV2Decorator : IScriptServiceV2
    {
        public long StartScriptCallCountStarted;
        public long GetStatusCallCountStarted;
        public long CancelScriptCallCountStarted;
        public long CompleteScriptCallCountStarted;
        
        public long StartScriptCallCountComplete;
        

        private IScriptServiceV2 inner;

        public CountingCallsScriptServiceV2Decorator(IScriptServiceV2 inner)
        {
            this.inner = inner;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
        {
            Interlocked.Increment(ref StartScriptCallCountStarted);
            try
            {
                return inner.StartScript(command);
            }
            finally
            {
                Interlocked.Increment(ref StartScriptCallCountComplete);
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            Interlocked.Increment(ref GetStatusCallCountStarted);
            return inner.GetStatus(request);
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            Interlocked.Increment(ref CancelScriptCallCountStarted);
            return inner.CancelScript(command);
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            Interlocked.Increment(ref CompleteScriptCallCountStarted);
            inner.CompleteScript(command);
        }
    }
}