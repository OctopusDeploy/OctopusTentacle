using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[TestFixture]
public class KubernetesScriptServiceV1AlphaIntegrationTest : KubernetesAgentIntegrationTest
{
    [Test]
    public async Task SimpleHelloWorld()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;
        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .SetScriptBody("echo \"Hello World\"")
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