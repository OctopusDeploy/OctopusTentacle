using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[TestFixture]
public class KubernetesScriptServiceV1IntegrationTest : KubernetesAgentIntegrationTest
{
    public KubernetesScriptServiceV1IntegrationTest()
    {
        TentacleServiceDecoratorBuilder = new TentacleServiceDecoratorBuilder()
            .DecorateCapabilitiesServiceV2With(d => d
                .DecorateGetCapabilitiesWith((inner, options) => Task.FromResult(new CapabilitiesResponseV2(new List<string> { nameof(IFileTransferService), nameof(IKubernetesScriptServiceV1) }))));
    }

    [Test]
    public async Task SimpleHelloWorld()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;
        string scriptBody = @"echo ""Hello World""
for i in $(seq 1 50);
do
    echo $i
    sleep 0.1
done
";
        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(scriptBody)
            .Build();

        //act
        var result = await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), CancellationToken.None);

        //Assert
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "Hello World");
        scriptCompleted.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.State.Should().Be(ProcessState.Complete);

        return;

        void StatusReceived(ScriptExecutionStatus status)
        {
            logs.AddRange(status.Logs);
        }

        Task ScriptCompleted(CancellationToken ct)
        {
            scriptCompleted = true;
            return Task.CompletedTask;
        }
    }
}