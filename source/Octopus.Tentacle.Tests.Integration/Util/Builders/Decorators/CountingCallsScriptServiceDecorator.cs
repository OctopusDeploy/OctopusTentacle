using System.Threading;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CountingCallsScriptServiceDecorator : IScriptService
    {
        public long StartScriptCallCountStarted;
        public long GetStatusCallCountStarted;
        public long CancelScriptCallCountStarted;
        public long CompleteScriptCallCountStarted;
        
        public long StartScriptCallCountComplete;
        
        private IScriptService inner;

        public CountingCallsScriptServiceDecorator(IScriptService inner)
        {
            this.inner = inner;
        }

        public ScriptTicket StartScript(StartScriptCommand command)
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

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            Interlocked.Increment(ref GetStatusCallCountStarted);
            return inner.GetStatus(request);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            Interlocked.Increment(ref CancelScriptCallCountStarted);
            return inner.CancelScript(command);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            Interlocked.Increment(ref CompleteScriptCallCountStarted);
            return inner.CompleteScript(command);
        }
    }
}