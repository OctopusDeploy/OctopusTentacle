using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
{
    public class KubernetesScriptServiceV1DecoratorBuilder
    {
        public delegate Task<KubernetesScriptStatusResponseV1> StartScriptClientDecorator(IAsyncClientKubernetesScriptServiceV1 inner, StartKubernetesScriptCommandV1 command, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task<KubernetesScriptStatusResponseV1> GetStatusClientDecorator(IAsyncClientKubernetesScriptServiceV1 inner, KubernetesScriptStatusRequestV1 request, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task<KubernetesScriptStatusResponseV1> CancelScriptClientDecorator(IAsyncClientKubernetesScriptServiceV1 inner, CancelKubernetesScriptCommandV1 command, HalibutProxyRequestOptions proxyRequestOptions);

        public delegate Task CompleteScriptClientDecorator(IAsyncClientKubernetesScriptServiceV1 inner, CompleteKubernetesScriptCommandV1 command, HalibutProxyRequestOptions proxyRequestOptions);

        private StartScriptClientDecorator startScriptFunc = async (inner, command, options) => await inner.StartScriptAsync(command, options);
        private GetStatusClientDecorator getStatusFunc = async (inner, command, options) => await inner.GetStatusAsync(command, options);
        private CancelScriptClientDecorator cancelScriptFunc = async (inner, command, options) => await inner.CancelScriptAsync(command, options);
        private CompleteScriptClientDecorator completeScriptAction = async (inner, command, options) => await inner.CompleteScriptAsync(command, options);

        public KubernetesScriptServiceV1DecoratorBuilder BeforeStartScript(Func<Task> beforeStartScript)
        {
            return BeforeStartScript(async (_, _, _) => await beforeStartScript());
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeStartScript(Func<IAsyncClientKubernetesScriptServiceV1, StartKubernetesScriptCommandV1, HalibutProxyRequestOptions, Task> beforeStartScript)
        {
            return DecorateStartScriptWith(async (inner, StartKubernetesScriptCommandV1, options) =>
            {
                await beforeStartScript(inner, StartKubernetesScriptCommandV1, options);
                return await inner.StartScriptAsync(StartKubernetesScriptCommandV1, options);
            });
        }

        public KubernetesScriptServiceV1DecoratorBuilder AfterStartScript(Func<Task> afterStartScript)
        {
            return AfterStartScript(async (_, _, _, _) => await afterStartScript());
        }

        public KubernetesScriptServiceV1DecoratorBuilder AfterStartScript(Func<IAsyncClientKubernetesScriptServiceV1, StartKubernetesScriptCommandV1, HalibutProxyRequestOptions, KubernetesScriptStatusResponseV1?, Task> afterStartScript)
        {
            return DecorateStartScriptWith(async (inner, StartKubernetesScriptCommandV1, options) =>
            {
                KubernetesScriptStatusResponseV1? response = null;
                try
                {
                    response = await inner.StartScriptAsync(StartKubernetesScriptCommandV1, options);
                }
                finally
                {
                    await afterStartScript(inner, StartKubernetesScriptCommandV1, options, response);
                }
                return response;
            });
        }

        public KubernetesScriptServiceV1DecoratorBuilder DecorateStartScriptWith(StartScriptClientDecorator startScriptFunc)
        {
            this.startScriptFunc = startScriptFunc;
            return this;
        }

        public KubernetesScriptServiceV1DecoratorBuilder DecorateGetStatusWith(GetStatusClientDecorator getStatusFunc)
        {
            this.getStatusFunc = getStatusFunc;
            return this;
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeGetStatus(Func<Task> beforeGetStatus)
        {
            return BeforeGetStatus(async (_, _, _) => await beforeGetStatus());
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeGetStatus(Func<IAsyncClientKubernetesScriptServiceV1, KubernetesScriptStatusRequestV1, HalibutProxyRequestOptions, Task> beforeGetStatus)
        {
            return DecorateGetStatusWith(async (inner, KubernetesScriptStatusRequestV1, options) =>
            {
                await beforeGetStatus(inner, KubernetesScriptStatusRequestV1, options);
                return await inner.GetStatusAsync(KubernetesScriptStatusRequestV1, options);
            });
        }

        public KubernetesScriptServiceV1DecoratorBuilder AfterGetStatus(Func<Task> afterGetStatus)
        {
            return AfterGetStatus(async (_, _, _, _) => await afterGetStatus());
        }

        public KubernetesScriptServiceV1DecoratorBuilder AfterGetStatus(Func<IAsyncClientKubernetesScriptServiceV1, KubernetesScriptStatusRequestV1, HalibutProxyRequestOptions, KubernetesScriptStatusResponseV1?, Task> afterGetStatus)
        {
            return DecorateGetStatusWith(async (inner, KubernetesScriptStatusRequestV1, options) =>
            {
                KubernetesScriptStatusResponseV1? response = null;
                try
                {
                    response = await inner.GetStatusAsync(KubernetesScriptStatusRequestV1, options);
                }
                finally
                {
                    await afterGetStatus(inner, KubernetesScriptStatusRequestV1, options, response);
                }
                return response;
            });
        }

        public KubernetesScriptServiceV1DecoratorBuilder DecorateCancelScriptWith(CancelScriptClientDecorator cancelScriptFunc)
        {
            this.cancelScriptFunc = cancelScriptFunc;
            return this;
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            return BeforeCancelScript(async (_, _, _) => await beforeCancelScript());
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeCancelScript(Func<IAsyncClientKubernetesScriptServiceV1, CancelKubernetesScriptCommandV1, HalibutProxyRequestOptions, Task> beforeCancelScript)
        {
            return DecorateCancelScriptWith(async (inner, command, options) =>
            {
                await beforeCancelScript(inner, command, options);
                return await inner.CancelScriptAsync(command, options);
            });
        }

        public KubernetesScriptServiceV1DecoratorBuilder DecorateCompleteScriptWith(CompleteScriptClientDecorator completeScriptAction)
        {
            this.completeScriptAction = completeScriptAction;
            return this;
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeCompleteScript(Func<Task> beforeCompleteScript)
        {
            return BeforeCompleteScript(async (_, _, _) => await beforeCompleteScript());
        }

        public KubernetesScriptServiceV1DecoratorBuilder BeforeCompleteScript(Func<IAsyncClientKubernetesScriptServiceV1, CompleteKubernetesScriptCommandV1, HalibutProxyRequestOptions, Task> beforeCompleteScript)
        {
            return DecorateCompleteScriptWith(async (inner, command, options) =>
            {
                await beforeCompleteScript(inner, command, options);
                await inner.CompleteScriptAsync(command, options);
            });
        }

        public Decorator<IAsyncClientKubernetesScriptServiceV1> Build()
        {
            return inner => new FuncDecoratingKubernetesScriptServiceV1(inner,
                startScriptFunc,
                getStatusFunc,
                cancelScriptFunc,
                completeScriptAction);
        }

        private class FuncDecoratingKubernetesScriptServiceV1 : IAsyncClientKubernetesScriptServiceV1
        {
            private readonly IAsyncClientKubernetesScriptServiceV1 inner;
            private readonly StartScriptClientDecorator startScriptFunc;
            private readonly GetStatusClientDecorator getStatusFunc;
            private readonly CancelScriptClientDecorator cancelScriptFunc;
            private readonly CompleteScriptClientDecorator completeScriptAction;

            public FuncDecoratingKubernetesScriptServiceV1(
                IAsyncClientKubernetesScriptServiceV1 inner,
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

            public async Task<KubernetesScriptStatusResponseV1> StartScriptAsync(StartKubernetesScriptCommandV1 command, HalibutProxyRequestOptions options)
            {
                return await startScriptFunc(inner, command, options);
            }

            public async Task<KubernetesScriptStatusResponseV1> GetStatusAsync(KubernetesScriptStatusRequestV1 request, HalibutProxyRequestOptions options)
            {
                return await getStatusFunc(inner, request, options);
            }

            public async Task<KubernetesScriptStatusResponseV1> CancelScriptAsync(CancelKubernetesScriptCommandV1 command, HalibutProxyRequestOptions options)
            {
                return await cancelScriptFunc(inner, command, options);
            }

            public async Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1 command, HalibutProxyRequestOptions options)
            {
                await completeScriptAction(inner, command, options);
            }
        }
    }
}