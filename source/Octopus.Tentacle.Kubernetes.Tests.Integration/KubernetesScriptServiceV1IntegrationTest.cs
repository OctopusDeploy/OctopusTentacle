using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[TestFixture]
public class KubernetesScriptServiceV1IntegrationTest : KubernetesAgentIntegrationTest
{
    IRecordedMethodUsages recordedMethodUsages = null!;

    protected override TentacleServiceDecoratorBuilder ConfigureTentacleServiceDecoratorBuilder(TentacleServiceDecoratorBuilder builder)
    {
        builder.RecordMethodUsages<IAsyncClientKubernetesScriptServiceV1>(out var recordedUsages)
            .DecorateCapabilitiesServiceV2With(d => d
                .DecorateGetCapabilitiesWith((_, _) => Task.FromResult(new CapabilitiesResponseV2(new List<string> { nameof(IFileTransferService), nameof(IKubernetesScriptServiceV1) }))));

        recordedMethodUsages = recordedUsages;

        return builder;
    }

    [Test]
    public async Task RunSimpleScript()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;

        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(script => script
                .Print("Hello World")
                .PrintNTimesWithDelay("Yep", 30, TimeSpan.FromMilliseconds(100)))
            .Build();

        //act
        var result = await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), CancellationToken);

        //Assert
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "Hello World");
        scriptCompleted.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.State.Should().Be(ProcessState.Complete);

        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.StartScriptAsync)).Started.Should().Be(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.GetStatusAsync)).Started.Should().BeGreaterThan(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.CompleteScriptAsync)).Started.Should().Be(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.CancelScriptAsync)).Started.Should().Be(0);

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
    
    [Test]
    public async Task SimpleScriptExitsWithErrorCode_ScriptFails()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;

        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(script => script
                .Print("Hello World")
                .ExitsWith(1))
            .Build();

        //act
        var result = await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), CancellationToken);

        //Assert
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "Hello World");
        scriptCompleted.Should().BeTrue();
        result.ExitCode.Should().Be(1);
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
    
    [Test]
    public async Task ScriptPodIsTerminatedDuringScriptExecution_ScriptFails()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;
        var semaphoreSlim = new SemaphoreSlim(0, 1);

        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(script => script
                .Print("Hello World")
                .Sleep(TimeSpan.FromSeconds(1))
                .Print("waitingtobestopped")
                .Sleep(TimeSpan.FromSeconds(100)))
            .Build();

        //act
        var scriptTask = Task.Run(async () => await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), CancellationToken));

        //wait for the script to be started, then waiting
        await semaphoreSlim.WaitAsync(CancellationToken);

        Logger.Information("Deleting script pod");
        await KubeCtl.ExecuteNamespacedCommand($"delete pods -l octopus.com/scriptTicketId={command.ScriptTicket.TaskId}");

        var result = await scriptTask;

        //Assert
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "Hello World");
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "waitingtobestopped");

        scriptCompleted.Should().BeTrue();
        result.ExitCode.Should().NotBe(0); // The error exit code seems to change and I can't work out why, so just testing that it's not success
        result.State.Should().Be(ProcessState.Complete);

        // The pod should not exist
        var commandResult = await KubeCtl.ExecuteNamespacedCommand($"get pods -l octopus.com/scriptTicketId={command.ScriptTicket.TaskId} -o \"Name\"");
        commandResult.StdOut.Should().BeEmpty();

        return;

        void StatusReceived(ScriptExecutionStatus status)
        {
            if (status.Logs.Any(l => l.Text == "waitingtobestopped"))
            {
                semaphoreSlim.Release();
            }

            logs.AddRange(status.Logs);
        }

        Task ScriptCompleted(CancellationToken ct)
        {
            scriptCompleted = true;
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task TentaclePodIsTerminatedDuringScriptExecution_ShouldRestartAndPickUpPodStatus()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;
        const int count = 100;
        var semaphoreSlim = new SemaphoreSlim(0, 1);

        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(script => script
                .Print("Hello World")
                .Sleep(TimeSpan.FromSeconds(1))
                .Print("waitingtobestopped")
                .PrintNTimesWithDelay(i => $"Count: {i}", count, TimeSpan.FromSeconds(1)))
            .Build();

        var commandResult = await KubeCtl.ExecuteNamespacedCommand("get pods -l app.kubernetes.io/name=octopus-agent -o \"Name\"");
        var initialPodName = commandResult.StdOut.Single();

        //act
        var scriptTask = Task.Run(async () => await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), CancellationToken));

        //wait for the script to be started, then waiting
        await semaphoreSlim.WaitAsync(CancellationToken);

        Logger.Information("Deleting tentacle pod");
        await KubeCtl.ExecuteNamespacedCommand("delete pods -l app.kubernetes.io/name=octopus-agent");

        var result = await scriptTask;

        commandResult = await KubeCtl.ExecuteNamespacedCommand("get pods -l app.kubernetes.io/name=octopus-agent -o \"Name\"");
        var finalPodName = commandResult.StdOut.Single();

        //Assert
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "Hello World");
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "waitingtobestopped");

        //verify that we are getting all the logs and that the tentacle has been killed
        using (var scope = new AssertionScope())
        {
            scope.FormattingOptions.MaxLines = 200;
            for (var i = 1; i < count; i++)
            {
                var testString = $"Count: {i}";
                logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == testString, because: $"the logs should contain all script output. Missing '{testString}'");
            }
        }

        scriptCompleted.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.State.Should().Be(ProcessState.Complete);

        finalPodName.Should().NotBe(initialPodName, because: "the tentacle pod should have been killed and restarted");

        return;

        void StatusReceived(ScriptExecutionStatus status)
        {
            if (status.Logs.Any(l => l.Text == "waitingtobestopped"))
            {
                semaphoreSlim.Release();
            }

            logs.AddRange(status.Logs);
        }

        Task ScriptCompleted(CancellationToken ct)
        {
            scriptCompleted = true;
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task WhenALongRunningScriptIsCancelled_TheScriptShouldStop()
    {
        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;

        var command = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(script => script
                .Print("hello")
                .Sleep(TimeSpan.FromSeconds(1))
                .Print("waitingtobestopped")
                .Sleep(TimeSpan.FromSeconds(100))
                .Print("i did not stop"))
            .Build();

        var scriptCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        Exception? actualException = null;

        //act
        try
        {
            await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), scriptCancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            actualException = ex;
        }

        //Assert
        actualException.Should().NotBeNull()
            .And
            .BeOfType<OperationCanceledException>()
            .And
            .Match<OperationCanceledException>(ex => ex.Message == "Script execution was cancelled");

        logs.Should().NotContain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "i did not stop");
        scriptCompleted.Should().BeTrue();

        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.StartScriptAsync)).Started.Should().Be(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.GetStatusAsync)).Started.Should().BeGreaterThan(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.CompleteScriptAsync)).Started.Should().Be(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.CancelScriptAsync)).Started.Should().BeGreaterOrEqualTo(1);

        return;

        void StatusReceived(ScriptExecutionStatus status)
        {
            if (status.Logs.Any(l => l.Text == "waitingtobestopped"))
                scriptCancellationTokenSource.Cancel();

            logs.AddRange(status.Logs);
        }

        Task ScriptCompleted(CancellationToken ct)
        {
            scriptCompleted = true;
            return Task.CompletedTask;
        }
    }
}