using k8s;
using k8s.Models;
using Octopus.Pipelines.Runner.Kubernetes;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class PodTrackerTests
{
    static readonly DateTime StartedAt = new(2026, 11, 2, 1, 2, 3, DateTimeKind.Utc);

    [Fact]
    public async Task Running_container_completes_started_but_not_terminated()
    {
        var tracker = new PodTracker();
        tracker.Observe(WatchEventType.Modified, Pod(new V1ContainerState { Running = new V1ContainerStateRunning { StartedAt = StartedAt } }));

        Assert.True(tracker.Started.IsCompletedSuccessfully);
        Assert.Equal(new DateTimeOffset(StartedAt), await tracker.Started);
        Assert.False(tracker.Terminated.IsCompleted);
    }

    [Fact]
    public async Task Container_that_finished_before_we_saw_it_running_completes_both()
    {
        var tracker = new PodTracker();
        tracker.Observe(WatchEventType.Modified, Pod(new V1ContainerState
        {
            Terminated = new V1ContainerStateTerminated { ExitCode = 7, StartedAt = StartedAt, FinishedAt = StartedAt.AddSeconds(1) },
        }));

        Assert.Equal(new DateTimeOffset(StartedAt), await tracker.Started);
        Assert.Equal(7, await tracker.Terminated);
    }

    [Fact]
    public void Image_pull_failure_fails_the_job()
    {
        var tracker = new PodTracker();
        tracker.Observe(WatchEventType.Modified, Pod(new V1ContainerState { Waiting = new V1ContainerStateWaiting { Reason = "ImagePullBackOff", Message = "nope" } }));

        Assert.True(tracker.Started.IsFaulted);
        Assert.True(tracker.Terminated.IsFaulted);
    }

    [Fact]
    public void Pending_pod_waiting_for_a_node_does_not_fail()
    {
        var tracker = new PodTracker();
        tracker.Observe(WatchEventType.Added, new V1Pod { Status = new V1PodStatus { Phase = "Pending" } });

        Assert.False(tracker.Started.IsCompleted);
    }

    [Fact]
    public void Reports_waiting_for_a_node_while_unschedulable_then_placed_once_scheduled()
    {
        var tracker = new PodTracker();
        var reports = new List<bool>();
        tracker.PlacementChanged = reports.Add;

        tracker.Observe(WatchEventType.Added, Scheduling("False", "Unschedulable"));
        tracker.Observe(WatchEventType.Modified, Scheduling("False", "Unschedulable"));
        tracker.Observe(WatchEventType.Modified, Scheduling("True"));
        tracker.Observe(WatchEventType.Modified, Scheduling("True"));

        Assert.Equal([true, false], reports);
    }

    [Fact]
    public void A_pod_scheduled_straight_away_is_reported_placed_without_waiting()
    {
        var tracker = new PodTracker();
        var reports = new List<bool>();
        tracker.PlacementChanged = reports.Add;

        tracker.Observe(WatchEventType.Added, new V1Pod { Status = new V1PodStatus { Phase = "Pending" } });
        tracker.Observe(WatchEventType.Modified, Scheduling("True"));

        Assert.Equal([false], reports);
    }

    static V1Pod Scheduling(string status, string? reason = null) => new()
    {
        Status = new V1PodStatus
        {
            Phase = "Pending",
            Conditions = [new V1PodCondition { Type = "PodScheduled", Status = status, Reason = reason }],
        },
    };

    [Fact]
    public void Deleted_pod_fails_the_job()
    {
        var tracker = new PodTracker();
        tracker.Observe(WatchEventType.Deleted, new V1Pod { Status = new V1PodStatus { Phase = "Pending" } });

        Assert.True(tracker.Terminated.IsFaulted);
    }

    static V1Pod Pod(V1ContainerState state) => new()
    {
        Status = new V1PodStatus
        {
            Phase = "Running",
            ContainerStatuses = [new V1ContainerStatus { Name = PodWatcher.ContainerName, State = state }],
        },
    };
}
