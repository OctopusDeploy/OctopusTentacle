using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV2DecoratorBuilder
    {
        public delegate ScriptStatusResponseV2 StartScriptClientDecorator(IClientScriptServiceV2 inner,StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate ScriptStatusResponseV2 GetStatusClientDecorator(IClientScriptServiceV2 inner,ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate ScriptStatusResponseV2 CancelScriptClientDecorator(IClientScriptServiceV2 inner,CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate void CompleteScriptClientDecorator(IClientScriptServiceV2 inner,CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        
        private StartScriptClientDecorator startScriptFunc = (inner, command, options) => inner.StartScript(command, options);
        private GetStatusClientDecorator getStatusFunc = (inner, command, options) => inner.GetStatus(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = (inner, command, options) => inner.CancelScript(command, options);
        private CompleteScriptClientDecorator completeScriptAction = (inner, command, options) => { };

        public ScriptServiceV2DecoratorBuilder BeforeStartScript(Action beforeStartScript)
        {
            return BeforeStartScript((_, _) => beforeStartScript());
        }

        public ScriptServiceV2DecoratorBuilder BeforeStartScript(Action<IClientScriptServiceV2, StartScriptCommandV2> beforeStartScript)
        {
            return DecorateStartScriptWith((inner, scriptStatusRequestV2, options) =>
            {
                beforeStartScript(inner, scriptStatusRequestV2);
                return inner.StartScript(scriptStatusRequestV2, options);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateStartScriptWith(StartScriptClientDecorator startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder DecorateGetStatusWith(GetStatusClientDecorator getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeGetStatus(Action beforeGetStatus)
        {

            return BeforeGetStatus((_, _) => beforeGetStatus());
        }

        public ScriptServiceV2DecoratorBuilder BeforeGetStatus(Action<IClientScriptServiceV2, ScriptStatusRequestV2> beforeGetStatus)
        {
            return DecorateGetStatusWith((inner, scriptStatusRequestV2, options) =>
            {
                beforeGetStatus(inner, scriptStatusRequestV2);
                return inner.GetStatus(scriptStatusRequestV2, options);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeCancelScript(Action beforeCancelScript)
        {
            return BeforeCancelScript((_, _) => beforeCancelScript());
        }

        public ScriptServiceV2DecoratorBuilder BeforeCancelScript(Action<IClientScriptServiceV2, CancelScriptCommandV2> beforeCancelScript)
        {
            return DecorateCancelScriptWith((inner, command, options) =>
            {
                beforeCancelScript(inner, command);
                return inner.CancelScript(command, options);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeCompleteScript(Action beforeCompleteScript)
        {
            return BeforeCompleteScript((_, _) => beforeCompleteScript());
        }

        public ScriptServiceV2DecoratorBuilder BeforeCompleteScript(Action<IClientScriptServiceV2, CompleteScriptCommandV2> beforeCompleteScript)
        {
            return DecorateCompleteScriptWith((inner, command, options) =>
            {
                beforeCompleteScript(inner, command);
                inner.CompleteScript(command, options);
            });
        }

        public Func<IClientScriptServiceV2, IClientScriptServiceV2> Build()
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


        private class FuncDecoratingScriptServiceV2 : IClientScriptServiceV2
        {
            private IClientScriptServiceV2 inner;
            private StartScriptClientDecorator startScriptFunc;
            private GetStatusClientDecorator getStatusFunc;
            private CancelScriptClientDecorator cancelScriptFunc;
            private CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingScriptServiceV2(
                IClientScriptServiceV2 inner,
                StartScriptClientDecorator startScriptFunc,
                GetStatusClientDecorator getStatusFunc,
                CancelScriptClientDecorator cancelScriptFunc,
                CompleteScriptClientDecorator completeScriptAction)
            {
                this.inner = inner;
                this.startScriptFunc = startScriptFunc;
                this.getStatusFunc = getStatusFunc;
                this.cancelScriptFunc = cancelScriptFunc;
                this.completeScriptAction = completeScriptAction;
            }

            public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions options)
            {
                return startScriptFunc(inner, command, options);
            }

            public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions options)
            {
                return getStatusFunc(inner, request, options);
            }

            public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions options)
            {
                return cancelScriptFunc(inner, command, options);
            }

            public void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions options)
            {
                completeScriptAction(inner, command, options);
            }
        }

    }
}