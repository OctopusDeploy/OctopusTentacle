using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV2DecoratorBuilder
    {
        public delegate Task<ScriptStatusResponseV2> StartScriptClientDecorator(IAsyncClientScriptServiceV2 inner,StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate Task<ScriptStatusResponseV2> GetStatusClientDecorator(IAsyncClientScriptServiceV2 inner,ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate Task<ScriptStatusResponseV2> CancelScriptClientDecorator(IAsyncClientScriptServiceV2 inner,CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate Task CompleteScriptClientDecorator(IAsyncClientScriptServiceV2 inner,CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        
        private StartScriptClientDecorator startScriptFunc = async (inner, command, options) => await inner.StartScriptAsync(command, options);
        private GetStatusClientDecorator getStatusFunc = async (inner, command, options) => await inner.GetStatusAsync(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = async (inner, command, options) => await inner.CancelScriptAsync(command, options);
        private CompleteScriptClientDecorator completeScriptAction = async (inner, command, options) => { await inner.CompleteScriptAsync(command, options); };

        public ScriptServiceV2DecoratorBuilder BeforeStartScript(Func<Task> beforeStartScript)
        {
            return BeforeStartScript(async (_, _, _) => await beforeStartScript());
        }

        public ScriptServiceV2DecoratorBuilder BeforeStartScript(Func<IAsyncClientScriptServiceV2, StartScriptCommandV2, HalibutProxyRequestOptions, Task> beforeStartScript)
        {
            return DecorateStartScriptWith(async (inner, scriptStatusRequestV2, options) =>
            {
                await beforeStartScript(inner, scriptStatusRequestV2, options);
                return await inner.StartScriptAsync(scriptStatusRequestV2, options);
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

        public ScriptServiceV2DecoratorBuilder BeforeGetStatus(Func<Task> beforeGetStatus)
        {

            return BeforeGetStatus(async (_, _, _) => await beforeGetStatus());
        }

        public ScriptServiceV2DecoratorBuilder BeforeGetStatus(Func<IAsyncClientScriptServiceV2, ScriptStatusRequestV2, HalibutProxyRequestOptions, Task> beforeGetStatus)
        {
            return DecorateGetStatusWith(async (inner, scriptStatusRequestV2, options) =>
            {
                await beforeGetStatus(inner, scriptStatusRequestV2, options);
                return await inner.GetStatusAsync(scriptStatusRequestV2, options);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            return BeforeCancelScript(async (_, _, _) => await beforeCancelScript());
        }

        public ScriptServiceV2DecoratorBuilder BeforeCancelScript(Func<IAsyncClientScriptServiceV2, CancelScriptCommandV2, HalibutProxyRequestOptions, Task> beforeCancelScript)
        {
            return DecorateCancelScriptWith(async (inner, command, options) =>
            {
                await beforeCancelScript(inner, command, options);
                return await inner.CancelScriptAsync(command, options);
            });
        }

        public ScriptServiceV2DecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceV2DecoratorBuilder BeforeCompleteScript(Func<Task> beforeCompleteScript)
        {
            return BeforeCompleteScript(async (_, _, _) => await beforeCompleteScript());
        }

        public ScriptServiceV2DecoratorBuilder BeforeCompleteScript(Func<IAsyncClientScriptServiceV2, CompleteScriptCommandV2, HalibutProxyRequestOptions, Task> beforeCompleteScript)
        {
            return DecorateCompleteScriptWith(async (inner, command, options) =>
            {
                await beforeCompleteScript(inner, command, options);
                await inner.CompleteScriptAsync(command, options);
            });
        }

        public Decorator<IAsyncClientScriptServiceV2> Build()
        {
            return inner => new FuncDecoratingScriptServiceV2(inner,
                startScriptFunc,
                getStatusFunc,
                cancelScriptFunc,
                completeScriptAction);
        }


        private class FuncDecoratingScriptServiceV2 : IAsyncClientScriptServiceV2
        {
            private IAsyncClientScriptServiceV2 inner;
            private StartScriptClientDecorator startScriptFunc;
            private GetStatusClientDecorator getStatusFunc;
            private CancelScriptClientDecorator cancelScriptFunc;
            private CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingScriptServiceV2(
                IAsyncClientScriptServiceV2 inner,
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

            public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions options)
            {
                return await startScriptFunc(inner, command, options);
            }

            public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions options)
            {
                return await getStatusFunc(inner, request, options);
            }

            public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions options)
            {
                return await cancelScriptFunc(inner, command, options);
            }

            public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions options)
            {
                await completeScriptAction(inner, command, options);
            }
        }

    }
}