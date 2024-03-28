using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class KubernetesScriptServiceV1AlphaDecoratorBuilder
    {
        public delegate Task<KubernetesScriptStatusResponseV1Alpha> StartScriptClientDecorator(IAsyncClientKubernetesScriptServiceV1Alpha inner, StartKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task<KubernetesScriptStatusResponseV1Alpha> GetStatusClientDecorator(IAsyncClientKubernetesScriptServiceV1Alpha inner, KubernetesScriptStatusRequestV1Alpha request, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptClientDecorator(IAsyncClientKubernetesScriptServiceV1Alpha inner, CancelKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task CompleteScriptClientDecorator(IAsyncClientKubernetesScriptServiceV1Alpha inner, CompleteKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);

        private StartScriptClientDecorator startScriptFunc = async (inner, command, options) => await inner.StartScriptAsync(command, options);
        private GetStatusClientDecorator getStatusFunc = async (inner, command, options) => await inner.GetStatusAsync(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = async (inner, command, options) => await inner.CancelScriptAsync(command, options);
        private CompleteScriptClientDecorator completeScriptAction = async (inner, command, options) => await inner.CompleteScriptAsync(command, options);

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeStartScript(Func<Task> beforeStartScript)
        {
            return BeforeStartScript(async (_, _, _) => await beforeStartScript());
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeStartScript(Func<IAsyncClientKubernetesScriptServiceV1Alpha, StartKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, Task> beforeStartScript)
        {
            return DecorateStartScriptWith(async (inner, StartKubernetesScriptCommandV1Alpha, options) =>
            {
                await beforeStartScript(inner, StartKubernetesScriptCommandV1Alpha, options);
                return await inner.StartScriptAsync(StartKubernetesScriptCommandV1Alpha, options);
            });
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder AfterStartScript(Func<Task> afterStartScript)
        {
            return AfterStartScript(async (_, _, _, _) => await afterStartScript());
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder AfterStartScript(Func<IAsyncClientKubernetesScriptServiceV1Alpha, StartKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, KubernetesScriptStatusResponseV1Alpha, Task> afterStartScript)
        {
            return DecorateStartScriptWith(async (inner, StartKubernetesScriptCommandV1Alpha, options) =>
            {
                KubernetesScriptStatusResponseV1Alpha response = null;
                try
                {
                    response = await inner.StartScriptAsync(StartKubernetesScriptCommandV1Alpha, options);
                }
                finally
                {
                    await afterStartScript(inner, StartKubernetesScriptCommandV1Alpha, options, response);
                }
                return response;
            });
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder DecorateStartScriptWith(StartScriptClientDecorator startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder DecorateGetStatusWith(GetStatusClientDecorator getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeGetStatus(Func<Task> beforeGetStatus)
        {
            return BeforeGetStatus(async (_, _, _) => await beforeGetStatus());
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeGetStatus(Func<IAsyncClientKubernetesScriptServiceV1Alpha, KubernetesScriptStatusRequestV1Alpha, HalibutProxyRequestOptions, Task> beforeGetStatus)
        {
            return DecorateGetStatusWith(async (inner, KubernetesScriptStatusRequestV1Alpha, options) =>
            {
                await beforeGetStatus(inner, KubernetesScriptStatusRequestV1Alpha, options);
                return await inner.GetStatusAsync(KubernetesScriptStatusRequestV1Alpha, options);
            });
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder AfterGetStatus(Func<Task> afterGetStatus)
        {
            return AfterGetStatus(async (_, _, _, _) => await afterGetStatus());
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder AfterGetStatus(Func<IAsyncClientKubernetesScriptServiceV1Alpha, KubernetesScriptStatusRequestV1Alpha, HalibutProxyRequestOptions, KubernetesScriptStatusResponseV1Alpha, Task> afterGetStatus)
        {
            return DecorateGetStatusWith(async (inner, KubernetesScriptStatusRequestV1Alpha, options) =>
            {
                KubernetesScriptStatusResponseV1Alpha response = null;
                try
                {
                    response = await inner.GetStatusAsync(KubernetesScriptStatusRequestV1Alpha, options);
                }
                finally
                {
                    await afterGetStatus(inner, KubernetesScriptStatusRequestV1Alpha, options, response);
                }
                return response;
            });
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            return BeforeCancelScript(async (_, _, _) => await beforeCancelScript());
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeCancelScript(Func<IAsyncClientKubernetesScriptServiceV1Alpha, CancelKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, Task> beforeCancelScript)
        {
            return DecorateCancelScriptWith(async (inner, command, options) =>
            {
                await beforeCancelScript(inner, command, options);
                return await inner.CancelScriptAsync(command, options);
            });
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeCompleteScript(Func<Task> beforeCompleteScript)
        {
            return BeforeCompleteScript(async (_, _, _) => await beforeCompleteScript());
        }

        public KubernetesScriptServiceV1AlphaDecoratorBuilder BeforeCompleteScript(Func<IAsyncClientKubernetesScriptServiceV1Alpha, CompleteKubernetesScriptCommandV1Alpha, HalibutProxyRequestOptions, Task> beforeCompleteScript)
        {
            return DecorateCompleteScriptWith(async (inner, command, options) =>
            {
                await beforeCompleteScript(inner, command, options);
                await inner.CompleteScriptAsync(command, options);
            });
        }

        public Decorator<IAsyncClientKubernetesScriptServiceV1Alpha> Build()
        {
            return inner => new FuncDecoratingKubernetesScriptServiceV1Alpha(inner,
                startScriptFunc,
                getStatusFunc,
                cancelScriptFunc,
                completeScriptAction);
        }

        private class FuncDecoratingKubernetesScriptServiceV1Alpha : IAsyncClientKubernetesScriptServiceV1Alpha
        {
            private readonly IAsyncClientKubernetesScriptServiceV1Alpha inner;
            private readonly StartScriptClientDecorator startScriptFunc;
            private readonly GetStatusClientDecorator getStatusFunc;
            private readonly CancelScriptClientDecorator cancelScriptFunc;
            private readonly CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingKubernetesScriptServiceV1Alpha(
                IAsyncClientKubernetesScriptServiceV1Alpha inner,
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