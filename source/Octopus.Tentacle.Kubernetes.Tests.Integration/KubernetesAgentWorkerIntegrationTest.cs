using FluentAssertions;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public static class KubernetesAgentWorkerIntegrationTest
{
    public abstract class BaseScriptPodImageIntegrationTest : KubernetesAgentIntegrationTest
    {
        public async Task<List<ProcessOutput>> ExecuteBasicScriptOperation()
        {
            // Arrange
            var logs = new List<ProcessOutput>();
            var scriptCompleted = false;

            var builder = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
                .WithScriptBody(script => script
                    .Print("Hello World")
                    .PrintNTimesWithDelay("Yep", 30, TimeSpan.FromMilliseconds(100)));
            
            var command = builder.Build();

            //Act
            await TentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), CancellationToken);

            return logs;
            
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
    
    public class ByDefaultUsesWorkerToolsImage : BaseScriptPodImageIntegrationTest
    {
        public ByDefaultUsesWorkerToolsImage()
        {
            CustomHelmValues.Add("agent.worker.enabled", "true");
            CustomHelmValues.Add("agent.deploymentTarget.enabled", "false");
        }

        [Test]
        public async Task ScriptPodSpawnedWithWorkerTools()
        {
            var logs = await ExecuteBasicScriptOperation();
            logs.Should().Contain(po => po.Source == ProcessOutputSource.Debug && po.Text == "octopusdeploy/worker-tools");
        }
    }

    public class ByDefaultDeploymentTargetUsesTheKubernetesAgentToolsImage : BaseScriptPodImageIntegrationTest
    {
        public ByDefaultDeploymentTargetUsesTheKubernetesAgentToolsImage()
        {
            CustomHelmValues.Add("agent.worker.enabled", "false");
            CustomHelmValues.Add("agent.deploymentTarget.enabled", "true");
        }
        
        [Test]
        public async Task ScriptPodSpawnedWithWorkerTools()
        {
            var logs = await ExecuteBasicScriptOperation();
            logs.Should().Contain(po => po.Source == ProcessOutputSource.Debug && po.Text == "octopusdeploy/kubernetes-agent-tools-base");
        }
    }

    public class WorkerImageCanBeOverridenViaHelmValues : BaseScriptPodImageIntegrationTest
    {
        readonly string ReplacementImageName = "nginx";
        public WorkerImageCanBeOverridenViaHelmValues()
        {
            CustomHelmValues.Add("agent.worker.enabled", "true");
            CustomHelmValues.Add("scriptPods.worker.image.repository", ReplacementImageName);
        }
        
        [Test]
        public async Task ScriptPodSpawnedWithImageDefinedInHelm()
        {
            var logs = await ExecuteBasicScriptOperation();
            logs.Should().Contain(po => po.Source == ProcessOutputSource.Debug && po.Text == ReplacementImageName);
        }
    }
}