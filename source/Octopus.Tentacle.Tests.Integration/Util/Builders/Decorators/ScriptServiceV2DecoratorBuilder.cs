using System;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV2DecoratorBuilder
    {
        private Func<IScriptServiceV2, StartScriptCommandV2, ScriptStatusResponseV2> startScriptFunc = (inner, command) => inner.StartScript(command);
        private Func<IScriptServiceV2, ScriptStatusRequestV2, ScriptStatusResponseV2> getStatusFunc = (inner, command) => inner.GetStatus(command);
        private Func<IScriptServiceV2, CancelScriptCommandV2, ScriptStatusResponseV2> cancelScriptFunc = (inner, command) => inner.CancelScript(command);
        private Action<IScriptServiceV2, CompleteScriptCommandV2> completeScriptAction = (inner, command) => { };

        public ScriptServiceV2DecoratorBuilder BeforeStartScript(Action beforeStartScript)
        {

            return DecorateStartScriptWith((inner, scriptStatusRequestV2) =>
            {
                beforeStartScript();
                return inner.StartScript(scriptStatusRequestV2);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateStartScriptWith(Func<IScriptServiceV2, StartScriptCommandV2, ScriptStatusResponseV2> startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder DecorateGetStatusWith(Func<IScriptServiceV2, ScriptStatusRequestV2, ScriptStatusResponseV2> getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeGetStatus(Action beforeGetStatus)
        {

            return DecorateGetStatusWith((inner, scriptStatusRequestV2) =>
            {
                beforeGetStatus();
                return inner.GetStatus(scriptStatusRequestV2);
            });
        }

        public ScriptServiceV2DecoratorBuilder BeforeGetStatus(Action<IScriptServiceV2, ScriptStatusRequestV2> beforeGetStatus)
        {
            return DecorateGetStatusWith((inner, scriptStatusRequestV2) =>
            {
                beforeGetStatus(inner, scriptStatusRequestV2);
                return inner.GetStatus(scriptStatusRequestV2);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateCancelScriptWith(Func<IScriptServiceV2, CancelScriptCommandV2, ScriptStatusResponseV2> cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeCancelScript(Action beforeCancelScript)
        {
            return DecorateCancelScriptWith((inner, command) =>
            {
                beforeCancelScript();
                return inner.CancelScript(command);
            });
        }

        public ScriptServiceV2DecoratorBuilder BeforeCancelScript(Action<IScriptServiceV2, CancelScriptCommandV2> beforeCancelScript)
        {
            return DecorateCancelScriptWith((inner, command) =>
            {
                beforeCancelScript(inner, command);
                return inner.CancelScript(command);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateCompleteScriptWith(Action<IScriptServiceV2, CompleteScriptCommandV2> completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeCompleteScript(Action beforeCompleteScript)
        {
            return DecorateCompleteScriptWith((inner, command) =>
            {
                beforeCompleteScript();
                inner.CompleteScript(command);
            });
        }

        public Func<IScriptServiceV2, IScriptServiceV2> Build()
        {
            return inner =>
            {
                return new FuncDecoratingScriptServiceV2(inner,
                    startScriptFunc,
                    getStatusFunc,
                    cancelScriptFunc,
                    completeScriptAction);
            };
        }


        private class FuncDecoratingScriptServiceV2 : IScriptServiceV2
        {
            private IScriptServiceV2 inner;
            private Func<IScriptServiceV2, StartScriptCommandV2, ScriptStatusResponseV2> startScriptFunc;
            private Func<IScriptServiceV2, ScriptStatusRequestV2, ScriptStatusResponseV2> getStatusFunc;
            private Func<IScriptServiceV2, CancelScriptCommandV2, ScriptStatusResponseV2> cancelScriptFunc;
            private Action<IScriptServiceV2, CompleteScriptCommandV2> completeScriptAction;

            public FuncDecoratingScriptServiceV2(
                IScriptServiceV2 inner,
                Func<IScriptServiceV2, StartScriptCommandV2, ScriptStatusResponseV2> startScriptFunc,
                Func<IScriptServiceV2, ScriptStatusRequestV2, ScriptStatusResponseV2> getStatusFunc,
                Func<IScriptServiceV2, CancelScriptCommandV2, ScriptStatusResponseV2> cancelScriptFunc,
                Action<IScriptServiceV2, CompleteScriptCommandV2> completeScriptAction)
            {
                this.inner = inner;
                this.startScriptFunc = startScriptFunc;
                this.getStatusFunc = getStatusFunc;
                this.cancelScriptFunc = cancelScriptFunc;
                this.completeScriptAction = completeScriptAction;
            }

            public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
            {
                return startScriptFunc(inner, command);
            }

            public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
            {
                return getStatusFunc(inner, request);
            }

            public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
            {
                return cancelScriptFunc(inner, command);
            }

            public void CompleteScript(CompleteScriptCommandV2 command)
            {
                completeScriptAction(inner, command);
            }
        }

    }
}