using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceDecoratorBuilder
    {
        public delegate ScriptTicket StartScriptClientDecorator(IClientScriptService inner, StartScriptCommand command, HalibutProxyRequestOptions options);
        public delegate ScriptStatusResponse GetStatusClientDecorator(IClientScriptService inner, ScriptStatusRequest request, HalibutProxyRequestOptions options);
        public delegate ScriptStatusResponse CancelScriptClientDecorator(IClientScriptService inner, CancelScriptCommand command, HalibutProxyRequestOptions options);
        public delegate ScriptStatusResponse CompleteScriptClientDecorator(IClientScriptService inner, CompleteScriptCommand command, HalibutProxyRequestOptions options);
        
        private StartScriptClientDecorator startScriptFunc = (inner, command, options) => inner.StartScript(command, options);
        private GetStatusClientDecorator getStatusFunc = (inner, command, options) => inner.GetStatus(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = (inner, command, options) => inner.CancelScript(command, options);
        private CompleteScriptClientDecorator completeScriptAction = (inner, command, options) => inner.CompleteScript(command, options);

        public ScriptServiceDecoratorBuilder BeforeStartScript(Action beforeGetStatus)
        {
            return DecorateStartScriptWith((inner, scriptStatusRequest, options) =>
            {
                beforeGetStatus();
                return inner.StartScript(scriptStatusRequest, options);
            });
        }
        
        public ScriptServiceDecoratorBuilder DecorateStartScriptWith(StartScriptClientDecorator startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder DecorateGetStatusWith(GetStatusClientDecorator getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeGetStatus(Action beforeGetStatus)
        {
            return BeforeGetStatus((_, _) => beforeGetStatus());
        }
        
        public ScriptServiceDecoratorBuilder BeforeGetStatus(Action<IClientScriptService, ScriptStatusRequest> beforeGetStatus)
        {
            return DecorateGetStatusWith((inner, scriptStatusRequest, options) =>
            {
                beforeGetStatus(inner, scriptStatusRequest);
                return inner.GetStatus(scriptStatusRequest, options);
            });
        }

        public ScriptServiceDecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeCancelScript(Action beforeCancelScript)
        {
            return DecorateCancelScriptWith((inner, command, options) =>
            {
                beforeCancelScript();
                return inner.CancelScript(command, options);
            });
        }

        public ScriptServiceDecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeCompleteScript(Action beforeCompleteScript)
        {
            return DecorateCompleteScriptWith((inner, command, options) =>
            {
                beforeCompleteScript();
                return inner.CompleteScript(command, options);
            });
        }

        public Func<IClientScriptService, IClientScriptService> Build()
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

        private class FuncDecoratingScriptService : IClientScriptService
        {
            private readonly IClientScriptService inner;
            private readonly StartScriptClientDecorator startScriptFunc;
            private readonly GetStatusClientDecorator getStatusFunc;
            private readonly CancelScriptClientDecorator cancelScriptFunc;
            private readonly CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingScriptService(
                IClientScriptService inner, StartScriptClientDecorator startScriptFunc, GetStatusClientDecorator getStatusFunc, CancelScriptClientDecorator cancelScriptFunc, CompleteScriptClientDecorator completeScriptAction)
            {
                this.inner = inner;
                this.startScriptFunc = startScriptFunc;
                this.getStatusFunc = getStatusFunc;
                this.cancelScriptFunc = cancelScriptFunc;
                this.completeScriptAction = completeScriptAction;
            }

            public ScriptTicket StartScript(StartScriptCommand command, HalibutProxyRequestOptions options)
            {
                return startScriptFunc(inner, command, options);
            }

            public ScriptStatusResponse GetStatus(ScriptStatusRequest request, HalibutProxyRequestOptions options)
            {
                return getStatusFunc(inner, request, options);
            }

            public ScriptStatusResponse CancelScript(CancelScriptCommand command, HalibutProxyRequestOptions options)
            {
                return cancelScriptFunc(inner, command, options);
            }

            public ScriptStatusResponse CompleteScript(CompleteScriptCommand command, HalibutProxyRequestOptions options)
            {
                return completeScriptAction(inner, command, options);
            }
        }
    }
}