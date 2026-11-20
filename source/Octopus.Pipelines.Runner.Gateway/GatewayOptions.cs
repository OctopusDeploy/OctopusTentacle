namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>The runner's connection to the Server cell and how much work it pulls. Bound from the <c>Runner</c> section.</summary>
public sealed class GatewayOptions
{
    /// <summary>
    /// gRPC endpoint of the Server cell: <c>https://</c> (TLS, with the platform's normal certificate validation) or
    /// <c>http://</c> (unencrypted h2c, e.g. http://20.1.2.3:8081).
    /// </summary>
    public string ServerAddress { get; set; } = "http://localhost:8081";

    /// <summary>Optional bearer token sent with every call. Empty sends none.</summary>
    public string Token { get; set; } = "";

    public string CellId { get; set; } = "local";

    /// <summary>Unique per runner process. Defaults to the pod name.</summary>
    public string RunnerId { get; set; } = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;

    /// <summary>
    /// Most jobs in flight at once. The runner pulls work until it reaches this. Used by
    /// <see cref="FixedCapacityProvider"/>; a cluster-aware provider could pull less.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 100;

    /// <summary>
    /// Most jobs in flight but not yet started (pods being scheduled or set up) at once. 0 means no cap.
    /// Paces pod starts so the backlog waits on the Server instead of on the nodes, where a burst
    /// overloads each node's CNI (Cilium answers 429 and the kubelet backs off).
    /// </summary>
    public int MaxStartingJobs { get; set; }

    /// <summary>Finds the starting cap from observed pod start delays instead of fixing it. See <see cref="AdaptiveCapacityProvider"/>.</summary>
    public AdaptiveStartOptions AdaptiveStarts { get; set; } = new();

    /// <summary>How often the runner re-checks its capacity when nothing has finished, in case the provider's view changed.</summary>
    public TimeSpan CapacityCheckInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Longest wait between reconnect attempts. The wait starts at a second and doubles after each failure up to this.
    /// </summary>
    public TimeSpan MaxReconnectBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Reconnect if the Server sends nothing (not even a heartbeat) for this long.</summary>
    public TimeSpan ServerSilenceTimeout { get; set; } = TimeSpan.FromSeconds(90);
}

/// <summary>
/// Tunes the starting cap like TCP congestion control: double it while starts are fast (until the first
/// back-off), then add <see cref="Step"/> per <see cref="AdjustInterval"/>, and halve it when the 90th
/// percentile start delay passes the target: <see cref="BaselineMultiplier"/> times the cell's normal start
/// time, and at least <see cref="TargetStartDelay"/>.
/// <see cref="GatewayOptions.MaxStartingJobs"/>, when above 0, is the ceiling; otherwise <see cref="GatewayOptions.MaxConcurrentJobs"/> is.
/// </summary>
public sealed class AdaptiveStartOptions
{
    public bool Enabled { get; set; }

    public int InitialLimit { get; set; } = 20;

    public int MinLimit { get; set; } = 4;

    public int Step { get; set; } = 4;

    /// <summary>
    /// The lowest start delay treated as congestion. The real target is <see cref="BaselineMultiplier"/> times this
    /// cell's normal start time, when that's higher. Delays count from assignment (including waiting for a
    /// create-pod slot), or from getting a node for pods that had to wait for one.
    /// </summary>
    public TimeSpan TargetStartDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How many times its normal start time a cell may take before that counts as congestion.</summary>
    public double BaselineMultiplier { get; set; } = 2;

    /// <summary>
    /// The normal start time is the fastest (10th percentile) starts seen over this window, so it can rise if
    /// the cell really got slower but sustained congestion can't talk it up.
    /// </summary>
    public TimeSpan BaselineWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How often the cap changes. Roughly one start delay, so each change shows up before the next.</summary>
    public TimeSpan AdjustInterval { get; set; } = TimeSpan.FromSeconds(5);
}
