using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV3AlphaDecoratorBuilder
    {
        public delegate Task<ScriptStatusResponseV3Alpha> StartScriptClientDecorator(IAsyncClientScriptServiceV3Alpha inner,StartScriptCommandV3Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate Task<ScriptStatusResponseV3Alpha> GetStatusClientDecorator(IAsyncClientScriptServiceV3Alpha inner,ScriptStatusRequestV3Alpha request, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate Task<ScriptStatusResponseV3Alpha> CancelScriptClientDecorator(IAsyncClientScriptServiceV3Alpha inner,CancelScriptCommandV3Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        public delegate Task CompleteScriptClientDecorator(IAsyncClientScriptServiceV3Alpha inner,CompleteScriptCommandV3Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        
        private StartScriptClientDecorator startScriptFunc = async (inner, command, options) => await inner.StartScriptAsync(command, options);
        private GetStatusClientDecorator getStatusFunc = async (inner, command, options) => await inner.GetStatusAsync(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = async (inner, command, options) => await inner.CancelScriptAsync(command, options);
        private CompleteScriptClientDecorator completeScriptAction = async (inner, command, options) => { await Task.CompletedTask; };

        public ScriptServiceV3AlphaDecoratorBuilder BeforeStartScript(Func<Task> beforeStartScript)
        {
            return BeforeStartScript(async (_, _) => await beforeStartScript());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeStartScript(Func<IAsyncClientScriptServiceV3Alpha, StartScriptCommandV3Alpha, Task> beforeStartScript)
        {
            return DecorateStartScriptWith(async (inner, scriptStatusRequestV3Alpha, options) =>
            {
                await beforeStartScript(inner, scriptStatusRequestV3Alpha);
                return await inner.StartScriptAsync(scriptStatusRequestV3Alpha, options);
            });
        }

        public ScriptServiceV3AlphaDecoratorBuilder DecorateStartScriptWith(StartScriptClientDecorator startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public ScriptServiceV3AlphaDecoratorBuilder DecorateGetStatusWith(GetStatusClientDecorator getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeGetStatus(Func<Task> beforeGetStatus)
        {

            return BeforeGetStatus(async (_, _) => await beforeGetStatus());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeGetStatus(Func<IAsyncClientScriptServiceV3Alpha, ScriptStatusRequestV3Alpha, Task> beforeGetStatus)
        {
            return DecorateGetStatusWith(async (inner, scriptStatusRequestV3Alpha, options) =>
            {
                await beforeGetStatus(inner, scriptStatusRequestV3Alpha);
                return await inner.GetStatusAsync(scriptStatusRequestV3Alpha, options);
            });
        }

        public ScriptServiceV3AlphaDecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            return BeforeCancelScript(async (_, _) => await beforeCancelScript());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCancelScript(Func<IAsyncClientScriptServiceV3Alpha, CancelScriptCommandV3Alpha, Task> beforeCancelScript)
        {
            return DecorateCancelScriptWith(async (inner, command, options) =>
            {
                await beforeCancelScript(inner, command);
                return await inner.CancelScriptAsync(command, options);
            });
        }

        public ScriptServiceV3AlphaDecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCompleteScript(Func<Task> beforeCompleteScript)
        {
            return BeforeCompleteScript(async (_, _, _) => await beforeCompleteScript());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCompleteScript(Func<IAsyncClientScriptServiceV3Alpha, CompleteScriptCommandV3Alpha, HalibutProxyRequestOptions, Task> beforeCompleteScript)
        {
            return DecorateCompleteScriptWith(async (inner, command, options) =>
            {
                await beforeCompleteScript(inner, command, options);
                await inner.CompleteScriptAsync(command, options);
            });
        }

        public Decorator<IAsyncClientScriptServiceV3Alpha> Build()
        {
            return inner => new FuncDecoratingScriptServiceV3Alpha(inner,
                startScriptFunc,
                getStatusFunc,
                cancelScriptFunc,
                completeScriptAction);
        }


        private class FuncDecoratingScriptServiceV3Alpha : IAsyncClientScriptServiceV3Alpha
        {
            private IAsyncClientScriptServiceV3Alpha inner;
            private StartScriptClientDecorator startScriptFunc;
            private GetStatusClientDecorator getStatusFunc;
            private CancelScriptClientDecorator cancelScriptFunc;
            private CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingScriptServiceV3Alpha(
                IAsyncClientScriptServiceV3Alpha inner,
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

            public async Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, HalibutProxyRequestOptions options)
            {
                return await startScriptFunc(inner, command, options);
            }

            public async Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, HalibutProxyRequestOptions options)
            {
                return await getStatusFunc(inner, request, options);
            }

            public async Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, HalibutProxyRequestOptions options)
            {
                return await cancelScriptFunc(inner, command, options);
            }

            public async Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, HalibutProxyRequestOptions options)
            {
                await completeScriptAction(inner, command, options);
            }
        }

    }
}