using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TestDecoratorsAreCalledInTheCorrectOrder
    {
        [Test]
        public async Task MethodDecoratorsAreCalledInTheOrderTheyAreDefined()
        {
            var list = new List<int>();
            var decorator = new TentacleServiceDecoratorBuilder()
                .DecorateScriptServiceV2With(b => b.BeforeCompleteScript(async (_, _, _) =>
                {
                    list.Add(1);
                    await Task.CompletedTask;
                }))
                .DecorateScriptServiceV2With(b => b.BeforeCompleteScript(async (_, _, _) =>
                {
                    list.Add(2);
                    await Task.CompletedTask;
                }))
                .DecorateScriptServiceV2With(b => b.DecorateCompleteScriptWith(async (inner, command, options) =>
                {
                    await Task.CompletedTask;
                    await inner.CompleteScriptAsync(command, options);
                    list.Add(3);
                }))
                .DecorateScriptServiceV2With(b => b.DecorateCompleteScriptWith(async (inner, command, options) =>
                {
                    await Task.CompletedTask;
                    await inner.CompleteScriptAsync(command, options);
                    list.Add(4);
                }))
                .Build();

            await decorator.Decorate(new NoOPClientScriptService()).CompleteScriptAsync(SomeCompleteScriptCommandV2(), SomeHalibutProxyRequestOptions());

            list.Should().BeEquivalentTo(new List<int> {1, 2, 3, 4});
        }

        [Test]
        public void ProxyGeneratedDecoratorsAreFirst()
        {
            var list = new List<int>();
            IRecordedMethodUsages recordedUsages = new MethodUsages();
            long startedCountInCall = 0;
            var decorator = new TentacleServiceDecoratorBuilder()
                .DecorateScriptServiceV2With(b => b.BeforeCompleteScript(async (_, _, _) =>
                    {
                        await Task.CompletedTask;
                        // We should find that the proxy decorator has already counted this call.
                        startedCountInCall = recordedUsages.ForAll().Started;
                        // If the proxy decorator after this then throwing an exception here would result in calls not being counted.
                        throw new Exception();
                    })
                    .Build())
                .RecordMethodUsages<IAsyncClientScriptServiceV2>(out recordedUsages)
                .Build();

            Assert.ThrowsAsync<Exception>(async () => await decorator.Decorate(new NoOPClientScriptService()).CompleteScriptAsync(SomeCompleteScriptCommandV2(), SomeHalibutProxyRequestOptions()));

            var usage = recordedUsages.ForAll();

            usage.LastException.Should().NotBeNull();
            usage.Started.Should().Be(1);
            startedCountInCall.Should().Be(1, "Because the proxy decorator which count calls should happen before any method specific decorators.");
        }

        static CompleteScriptCommandV2 SomeCompleteScriptCommandV2()
        {
            return new CompleteScriptCommandV2(new ScriptTicket("a"));
        }

        static HalibutProxyRequestOptions SomeHalibutProxyRequestOptions()
        {
            return new HalibutProxyRequestOptions(CancellationToken.None);
        }

        class NoOPClientScriptService : IAsyncClientScriptServiceV2
        {
            public Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
            {
                throw new NotImplementedException();
            }

            public Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions)
            {
                throw new NotImplementedException();
            }

            public Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
            {
                throw new NotImplementedException();
            }

            public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
            {
                await Task.CompletedTask;
            }
        }
    }
}