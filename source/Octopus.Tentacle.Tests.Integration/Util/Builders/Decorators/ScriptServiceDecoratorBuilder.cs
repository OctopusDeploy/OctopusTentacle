using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceDecoratorBuilder
    {
        private Func<IScriptService, StartScriptCommand, ScriptTicket> startScriptFunc = (inner, command) => inner.StartScript(command);
        private Func<IScriptService, ScriptStatusRequest, ScriptStatusResponse> getStatusFunc = (inner, command) => inner.GetStatus(command);
        private Func<IScriptService, CancelScriptCommand, ScriptStatusResponse> cancelScriptFunc = (inner, command) => inner.CancelScript(command);
        private Func<IScriptService, CompleteScriptCommand, ScriptStatusResponse> completeScriptAction = (inner, command) => inner.CompleteScript(command);

        public ScriptServiceDecoratorBuilder BeforeStartScript(Action beforeGetStatus)
        {
            return DecorateStartScriptWith((inner, scriptStatusRequest) =>
            {
                beforeGetStatus();
                return inner.StartScript(scriptStatusRequest);
            });
        }
        
        public ScriptServiceDecoratorBuilder DecorateStartScriptWith(Func<IScriptService, StartScriptCommand, ScriptTicket> startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder DecorateGetStatusWith(Func<IScriptService, ScriptStatusRequest, ScriptStatusResponse> getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeGetStatus(Action beforeGetStatus)
        {
            return DecorateGetStatusWith((inner, scriptStatusRequest) =>
            {
                beforeGetStatus();
                return inner.GetStatus(scriptStatusRequest);
            });
        }

        public ScriptServiceDecoratorBuilder DecorateCancelScriptWith(Func<IScriptService, CancelScriptCommand, ScriptStatusResponse> cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeCancelScript(Action beforeCancelScript)
        {
            return DecorateCancelScriptWith((inner, command) =>
            {
                beforeCancelScript();
                return inner.CancelScript(command);
            });
        }

        public ScriptServiceDecoratorBuilder DecorateCompleteScriptWith(Func<IScriptService, CompleteScriptCommand, ScriptStatusResponse> completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeCompleteScript(Action beforeCompleteScript)
        {
            return DecorateCompleteScriptWith((inner, command) =>
            {
                beforeCompleteScript();
                return inner.CompleteScript(command);
            });
        }

        public Func<IScriptService, IScriptService> Build()
        {
            return inner =>
            {
                return new FuncDecoratingScriptService(inner,
                    startScriptFunc,
                    getStatusFunc,
                    cancelScriptFunc,
                    completeScriptAction);
            };
        }

        private class FuncDecoratingScriptService : IScriptService
        {
            private readonly IScriptService inner;
            private readonly Func<IScriptService, StartScriptCommand, ScriptTicket> startScriptFunc;
            private readonly Func<IScriptService, ScriptStatusRequest, ScriptStatusResponse> getStatusFunc;
            private readonly Func<IScriptService, CancelScriptCommand, ScriptStatusResponse> cancelScriptFunc;
            private readonly Func<IScriptService, CompleteScriptCommand, ScriptStatusResponse> completeScriptAction;

            public FuncDecoratingScriptService(
                IScriptService inner, Func<IScriptService, StartScriptCommand, ScriptTicket> startScriptFunc, Func<IScriptService, ScriptStatusRequest, ScriptStatusResponse> getStatusFunc, Func<IScriptService, CancelScriptCommand, ScriptStatusResponse> cancelScriptFunc, Func<IScriptService, CompleteScriptCommand, ScriptStatusResponse> completeScriptAction)
            {
                this.inner = inner;
                this.startScriptFunc = startScriptFunc;
                this.getStatusFunc = getStatusFunc;
                this.cancelScriptFunc = cancelScriptFunc;
                this.completeScriptAction = completeScriptAction;
            }

            public ScriptTicket StartScript(StartScriptCommand command)
            {
                return startScriptFunc(inner, command);
            }

            public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
            {
                return getStatusFunc(inner, request);
            }

            public ScriptStatusResponse CancelScript(CancelScriptCommand command)
            {
                return cancelScriptFunc(inner, command);
            }

            public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
            {
                return completeScriptAction(inner, command);
            }
        }
    }
}