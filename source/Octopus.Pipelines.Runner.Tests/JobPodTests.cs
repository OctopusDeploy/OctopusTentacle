using k8s.Models;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Kubernetes;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>The job pod spec carries the isolation guarantees, so pin them down.</summary>
public sealed class JobPodTests
{
    static KubernetesBackendOptions Options(bool userNamespaces = true) => new()
    {
        UserNamespaces = userNamespaces,
        NodeSelector = new() { ["poc.octopus.com/pool"] = "jobs" },
        Tolerations = [new TolerationOptions { Key = "poc.octopus.com/jobs", Value = "true" }],
        Sandbox = new SandboxOptions
        {
            Enabled = true,
            NodeSelector = new() { ["poc.octopus.com/pool"] = "sandbox" },
            Tolerations = [new TolerationOptions { Key = "poc.octopus.com/sandbox", Value = "true" }],
        },
    };

    static V1Pod Build(JobIsolation isolation, KubernetesBackendOptions? options = null) =>
        KubernetesJobBackend.BuildPod("job-1", new JobSpec("1", "run-1", "echo hi", isolation), options ?? Options(), new GatewayOptions { RunnerId = "runner-1", CellId = "r1" });

    [Theory]
    [InlineData(JobIsolation.Standard)]
    [InlineData(JobIsolation.Sandboxed)]
    public void Every_job_pod_meets_pod_security_restricted_and_has_limits(JobIsolation isolation)
    {
        var pod = Build(isolation);
        var spec = pod.Spec;
        var container = Assert.Single(spec.Containers);

        Assert.False(spec.AutomountServiceAccountToken);
        Assert.Null(spec.HostNetwork);
        Assert.Null(spec.HostPID);
        Assert.Null(spec.HostIPC);
        Assert.Null(spec.Volumes);
        Assert.True(spec.SecurityContext.RunAsNonRoot);
        Assert.Equal("RuntimeDefault", spec.SecurityContext.SeccompProfile.Type);
        Assert.False(container.SecurityContext.AllowPrivilegeEscalation);
        Assert.False(container.SecurityContext.Privileged);
        Assert.True(container.SecurityContext.ReadOnlyRootFilesystem);
        Assert.Equal(["ALL"], container.SecurityContext.Capabilities.Drop);
        Assert.Null(container.SecurityContext.Capabilities.Add);

        Assert.Equal(["cpu", "ephemeral-storage", "memory"], container.Resources.Limits.Keys.Order());
    }

    [Fact]
    public void Standard_jobs_use_runc_a_user_namespace_and_the_jobs_pool()
    {
        var pod = Build(JobIsolation.Standard);

        Assert.Null(pod.Spec.RuntimeClassName);
        Assert.False(pod.Spec.HostUsers);
        Assert.Equal("jobs", pod.Spec.NodeSelector["poc.octopus.com/pool"]);
        Assert.Equal("poc.octopus.com/jobs", Assert.Single(pod.Spec.Tolerations).Key);
        Assert.Equal("standard", pod.Metadata.Labels["poc.octopus.com/isolation"]);
        Assert.Equal("250m", pod.Spec.Containers[0].Resources.Limits["cpu"].ToString());
        Assert.Equal("32Mi", pod.Spec.Containers[0].Resources.Limits["memory"].ToString());
    }

    [Fact]
    public void Sandboxed_jobs_use_kata_on_the_sandbox_pool_with_a_supported_pod_vm_size()
    {
        var pod = Build(JobIsolation.Sandboxed);

        Assert.Equal("kata-vm-isolation", pod.Spec.RuntimeClassName);
        // The pod VM has its own kernel; Kata doesn't support user namespaces inside it.
        Assert.Null(pod.Spec.HostUsers);
        Assert.Equal("sandbox", pod.Spec.NodeSelector["poc.octopus.com/pool"]);
        Assert.Equal("poc.octopus.com/sandbox", Assert.Single(pod.Spec.Tolerations).Key);
        Assert.Equal("sandboxed", pod.Metadata.Labels["poc.octopus.com/isolation"]);
        // AKS rejects pod VMs under 128Mi; the memory limit sizes the VM.
        Assert.Equal("256Mi", pod.Spec.Containers[0].Resources.Limits["memory"].ToString());
        // The pod VM uses its whole memory size, so the scheduler must reserve all of it.
        Assert.Equal("256Mi", pod.Spec.Containers[0].Resources.Requests["memory"].ToString());
        Assert.Equal("1", pod.Spec.Containers[0].Resources.Limits["cpu"].ToString());
    }

    [Fact]
    public void User_namespaces_can_be_turned_off_for_clusters_older_than_1_33()
    {
        Assert.Null(Build(JobIsolation.Standard, Options(userNamespaces: false)).Spec.HostUsers);
    }
}
