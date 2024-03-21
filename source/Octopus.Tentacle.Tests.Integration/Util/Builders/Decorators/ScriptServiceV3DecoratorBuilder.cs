using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class ScriptServiceV3AlphaDecoratorBuilder
    {
        public delegate Task<KubernetesScriptStatusResponseV1Alpha> StartScriptClientDecorator(IAsyncClientScriptServiceV3Alpha inner, StartKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task<KubernetesScriptStatusResponseV1Alpha> GetStatusClientDecorator(IAsyncClientScriptServiceV3Alpha inner, KubernetesScriptStatusRequestV1Alpha request, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptClientDecorator(IAsyncClientScriptServiceV3Alpha inner, CancelKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task CompleteScriptClientDecorator(IAsyncClientScriptServiceV3Alpha inner, CompleteKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);

        private StartScriptClientDecorator startScriptFunc = async (inner, command, options) => await inner.StartScriptAsync(command, options);
        private GetStatusClientDecorator getStatusFunc = async (inner, command, options) => await inner.GetStatusAsync(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = async (inner, command, options) => await inner.CancelScriptAsync(command, options);
        private CompleteScriptClientDecorator completeScriptAction = async (inner, command, options) => await inner.CompleteScriptAsync(command, options);

        public ScriptServiceV3AlphaDecoratorBuilder BeforeStartScript(Func<Task> beforeStartScript)
        {
            return BeforeStartScript(async (_, _, _) => await beforeStartScript());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeStartScript(Func<IAsyncClientScriptServiceV3Alpha, StartKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, Task> beforeStartScript)
        {
            return DecorateStartScriptWith(async (inner, startScriptCommandV3Alpha, options) =>
            {
                await beforeStartScript(inner, startScriptCommandV3Alpha, options);
                return await inner.StartScriptAsync(startScriptCommandV3Alpha, options);
            });
        }

        public ScriptServiceV3AlphaDecoratorBuilder AfterStartScript(Func<Task> afterStartScript)
        {
            return AfterStartScript(async (_, _, _, _) => await afterStartScript());
        }

        public ScriptServiceV3AlphaDecoratorBuilder AfterStartScript(Func<IAsyncClientScriptServiceV3Alpha, StartScriptCommandV3Alpha, HalibutProxyRequestOptions, ScriptStatusResponseV3Alpha, Task> afterStartScript)
        {
            return DecorateStartScriptWith(async (inner, startScriptCommandV3Alpha, options) =>
            {
                ScriptStatusResponseV3Alpha response = null;
                try
                {
                    response = await inner.StartScriptAsync(startScriptCommandV3Alpha, options);
                }
                finally
                {
                    await afterStartScript(inner, startScriptCommandV3Alpha, options, response);
                }
                return response;
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
            return BeforeGetStatus(async (_, _, _) => await beforeGetStatus());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeGetStatus(Func<IAsyncClientScriptServiceV3Alpha, KubernetesScriptStatusRequestV1Alpha, HalibutProxyRequestOptions, Task> beforeGetStatus)
        {
            return DecorateGetStatusWith(async (inner, scriptStatusRequestV3Alpha, options) =>
            {
                await beforeGetStatus(inner, scriptStatusRequestV3Alpha, options);
                return await inner.GetStatusAsync(scriptStatusRequestV3Alpha, options);
            });
        }

        public ScriptServiceV3AlphaDecoratorBuilder AfterGetStatus(Func<Task> afterGetStatus)
        {
            return AfterGetStatus(async (_, _, _, _) => await afterGetStatus());
        }

        public ScriptServiceV3AlphaDecoratorBuilder AfterGetStatus(Func<IAsyncClientScriptServiceV3Alpha, ScriptStatusRequestV3Alpha, HalibutProxyRequestOptions, ScriptStatusResponseV3Alpha, Task> afterGetStatus)
        {
            return DecorateGetStatusWith(async (inner, scriptStatusRequestV3Alpha, options) =>
            {
                ScriptStatusResponseV3Alpha response = null;
                try
                {
                    response = await inner.GetStatusAsync(scriptStatusRequestV3Alpha, options);
                }
                finally
                {
                    await afterGetStatus(inner, scriptStatusRequestV3Alpha, options, response);
                }
                return response;
            });
        }

        public ScriptServiceV3AlphaDecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            return BeforeCancelScript(async (_, _, _) => await beforeCancelScript());
        }

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCancelScript(Func<IAsyncClientScriptServiceV3Alpha, CancelKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, Task> beforeCancelScript)
        {
            return DecorateCancelScriptWith(async (inner, command, options) =>
            {
                await beforeCancelScript(inner, command, options);
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

        public ScriptServiceV3AlphaDecoratorBuilder BeforeCompleteScript(Func<IAsyncClientScriptServiceV3Alpha, CompleteKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, Task> beforeCompleteScript)
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
            private readonly IAsyncClientScriptServiceV3Alpha inner;
            private readonly StartScriptClientDecorator startScriptFunc;
            private readonly GetStatusClientDecorator getStatusFunc;
            private readonly CancelScriptClientDecorator cancelScriptFunc;
            private readonly CompleteScriptClientDecorator completeScriptAction;

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

            public async Task<KubernetesScriptStatusResponseV1Alpha> StartScriptAsync(StartKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions options)
            {
                return await startScriptFunc(inner, command, options);
            }

            public async Task<KubernetesScriptStatusResponseV1Alpha> GetStatusAsync(KubernetesScriptStatusRequestV1Alpha request, HalibutProxyRequestOptions options)
            {
                return await getStatusFunc(inner, request, options);
            }

            public async Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAsync(CancelKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions options)
            {
                return await cancelScriptFunc(inner, command, options);
            }

            public async Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions options)
            {
                await completeScriptAction(inner, command, options);
            }
        }
    }
}