using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceDecoratorBuilder
    {
        public delegate Task<ScriptTicket> StartScriptClientDecorator(IAsyncClientScriptService inner, StartScriptCommand command, HalibutProxyRequestOptions options);
        public delegate Task<ScriptStatusResponse> GetStatusClientDecorator(IAsyncClientScriptService inner, ScriptStatusRequest request, HalibutProxyRequestOptions options);
        public delegate Task<ScriptStatusResponse> CancelScriptClientDecorator(IAsyncClientScriptService inner, CancelScriptCommand command, HalibutProxyRequestOptions options);
        public delegate Task<ScriptStatusResponse> CompleteScriptClientDecorator(IAsyncClientScriptService inner, CompleteScriptCommand command, HalibutProxyRequestOptions options);
        
        private StartScriptClientDecorator startScriptFunc = async (inner, command, options) => await inner.StartScriptAsync(command, options);
        private GetStatusClientDecorator getStatusFunc = async (inner, command, options) => await inner.GetStatusAsync(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = async (inner, command, options) => await inner.CancelScriptAsync(command, options);
        private CompleteScriptClientDecorator completeScriptAction = async (inner, command, options) => await inner.CompleteScriptAsync(command, options);

        public ScriptServiceDecoratorBuilder BeforeStartScript(Func<Task> beforeGetStatus)
        {
            return DecorateStartScriptWith(async (inner, scriptStatusRequest, options) =>
            {
                await beforeGetStatus();
                return await inner.StartScriptAsync(scriptStatusRequest, options);
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

        public ScriptServiceDecoratorBuilder BeforeGetStatus(Func<Task> beforeGetStatus)
        {
            return BeforeGetStatus(async (_, _) => await beforeGetStatus());
        }
        
        public ScriptServiceDecoratorBuilder BeforeGetStatus(Func<IAsyncClientScriptService, ScriptStatusRequest, Task> beforeGetStatus)
        {
            return DecorateGetStatusWith(async (inner, scriptStatusRequest, options) =>
            {
                await beforeGetStatus(inner, scriptStatusRequest);
                return await inner.GetStatusAsync(scriptStatusRequest, options);
            });
        }

        public ScriptServiceDecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            return DecorateCancelScriptWith(async (inner, command, options) =>
            {
                await beforeCancelScript();
                return await inner.CancelScriptAsync(command, options);
            });
        }

        public ScriptServiceDecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public ScriptServiceDecoratorBuilder BeforeCompleteScript(Func<Task> beforeCompleteScript)
        {
            return DecorateCompleteScriptWith(async (inner, command, options) =>
            {
                await beforeCompleteScript();
                return await inner.CompleteScriptAsync(command, options);
            });
        }

        public Decorator<IAsyncClientScriptService> Build()
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

        private class FuncDecoratingScriptService : IAsyncClientScriptService
        {
            private readonly IAsyncClientScriptService inner;
            private readonly StartScriptClientDecorator startScriptFunc;
            private readonly GetStatusClientDecorator getStatusFunc;
            private readonly CancelScriptClientDecorator cancelScriptFunc;
            private readonly CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingScriptService(
                IAsyncClientScriptService inner, StartScriptClientDecorator startScriptFunc, GetStatusClientDecorator getStatusFunc, CancelScriptClientDecorator cancelScriptFunc, CompleteScriptClientDecorator completeScriptAction)
            {
                this.inner = inner;
                this.startScriptFunc = startScriptFunc;
                this.getStatusFunc = getStatusFunc;
                this.cancelScriptFunc = cancelScriptFunc;
                this.completeScriptAction = completeScriptAction;
            }

            public async Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, HalibutProxyRequestOptions options)
            {
                return await startScriptFunc(inner, command, options);
            }

            public async Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, HalibutProxyRequestOptions options)
            {
                return await getStatusFunc(inner, request, options);
            }

            public async Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, HalibutProxyRequestOptions options)
            {
                return await cancelScriptFunc(inner, command, options);
            }

            public async Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, HalibutProxyRequestOptions options)
            {
                return await completeScriptAction(inner, command, options);
            }
        }
    }
}