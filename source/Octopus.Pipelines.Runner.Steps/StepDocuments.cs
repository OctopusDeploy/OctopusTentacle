using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// What the job runner gives the step runner. They're passed as <c>KEY=VALUE</c> arguments to
/// <c>octopus-step-runner run</c> (a Kubernetes exec can't set environment variables), or as environment variables.
/// Scripts see only <see cref="Workspace"/> and <see cref="Output"/>.
/// </summary>
public static class StepEnvironment
{
    /// <summary>The job's shared workspace, and the step's working directory.</summary>
    public const string Workspace = "OCTOPUS_WORKSPACE";

    /// <summary>The file a step writes <c>name=value</c> outputs to. Isolated between steps.</summary>
    public const string Output = "OCTOPUS_OUTPUT";

    /// <summary>The step's payload, exactly as the Server sent it. Scripts don't see it.</summary>
    public const string Input = "OCTOPUS_INPUT";

    /// <summary>Where the step runner writes this step's <see cref="StepRunResult"/>. Scripts don't see it.</summary>
    public const string Result = "OCTOPUS_RESULT";

    /// <summary>
    /// The results of every earlier step, one <see cref="StepRunResult"/> per line. The job runner appends to it; steps
    /// only read it.
    /// </summary>
    public const string Results = "OCTOPUS_RESULTS";

    /// <summary><c>success</c>, <c>failure</c> or <c>cancelled</c>: the job's status as this step starts.</summary>
    public const string JobStatus = "OCTOPUS_JOB_STATUS";

    /// <summary>A file the job runner creates to stop the step (a timeout or a cancellation).</summary>
    public const string Cancel = "OCTOPUS_CANCEL";

    /// <summary>Set to the same paths as their OCTOPUS_ equivalents, so scripts written for GitHub Actions work.</summary>
    public const string GitHubWorkspace = "GITHUB_WORKSPACE";

    public const string GitHubOutput = "GITHUB_OUTPUT";

    /// <summary>The largest value one output may have, as in GitHub Actions.</summary>
    public const int MaxOutputBytes = 1024 * 1024;

    /// <summary>The most all of a job's outputs may add up to, as in GitHub Actions.</summary>
    public const long MaxJobOutputBytes = 50L * 1024 * 1024;
}

/// <summary>
/// A step's payload, version 1. The Server writes it and the step runner reads it; the job runner passes it through
/// without reading it. <see cref="Run"/>, <see cref="If"/>, <see cref="Name"/> and the <see cref="Env"/> values may
/// hold <c>${{ }}</c> expressions.
/// </summary>
public sealed class StepPayload
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string Id { get; set; } = "";

    public string? Name { get; set; }

    /// <summary>The step's condition, with or without <c>${{ }}</c>. Null means <c>success()</c>.</summary>
    public string? If { get; set; }

    /// <summary>How to run <see cref="Run"/>, e.g. <c>bash -e {0}</c>; <c>{0}</c> is the script's path. Null means the default.</summary>
    public string? Shell { get; set; }

    /// <summary>The step's environment, the job's included. Also the <c>env</c> context.</summary>
    public Dictionary<string, string> Env { get; set; } = [];

    /// <summary>The job's secrets, as <c>secrets.&lt;name&gt;</c>. Every value is masked in logs.</summary>
    public Dictionary<string, string> Secrets { get; set; } = [];

    public string Run { get; set; } = "";
}

/// <summary>Everything a step's expressions can refer to.</summary>
public sealed class StepContext
{
    public StepContextJob Job { get; set; } = new();

    /// <summary>As <c>env.&lt;name&gt;</c>.</summary>
    public Dictionary<string, string> Env { get; set; } = [];

    /// <summary>The steps that have finished, by id, as <c>steps.&lt;id&gt;</c>.</summary>
    public Dictionary<string, StepContextStep> Steps { get; set; } = [];

    /// <summary>As <c>secrets.&lt;name&gt;</c>.</summary>
    public Dictionary<string, string> Secrets { get; set; } = [];

    /// <summary>Values earlier steps asked to mask, with <c>::add-mask::</c>.</summary>
    public List<string> Masks { get; set; } = [];

    /// <summary>The context a step runs in: its payload's env and secrets, and the earlier steps' results.</summary>
    public static StepContext For(StepPayload payload, IEnumerable<StepRunResult> earlier, string jobStatus)
    {
        var context = new StepContext
        {
            Job = new StepContextJob { Status = jobStatus },
            Env = payload.Env,
            Secrets = payload.Secrets,
        };
        foreach (var result in earlier)
        {
            context.Steps[result.Id] = new StepContextStep { Outcome = result.Outcome, Outputs = result.Outputs };
            context.Masks.AddRange(result.Masks);
        }

        return context;
    }
}

public sealed class StepContextJob
{
    /// <summary><c>success</c>, <c>failure</c> or <c>cancelled</c>.</summary>
    public string Status { get; set; } = JobStatus.Success;
}

public static class JobStatus
{
    public const string Success = "success";
    public const string Failure = "failure";
    public const string Cancelled = "cancelled";
}

public sealed class StepContextStep
{
    public StepRunOutcome Outcome { get; set; }

    public Dictionary<string, string> Outputs { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<StepRunOutcome>))]
public enum StepRunOutcome
{
    [JsonStringEnumMemberName("success")]
    Success,

    [JsonStringEnumMemberName("failure")]
    Failure,

    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>Stopped by the job runner, for a timeout or a cancellation, before it finished.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}

/// <summary>
/// How a step ended. The step runner writes it to <c>OCTOPUS_RESULT</c>, and the job runner appends it, as one line, to
/// the results file later steps read.
/// </summary>
public sealed class StepRunResult
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string Id { get; set; } = "";

    public StepRunOutcome Outcome { get; set; }

    /// <summary>The script's exit code, or null when it was skipped or never started.</summary>
    public int? ExitCode { get; set; }

    /// <summary>What the step wrote to <c>OCTOPUS_OUTPUT</c>, kept even when the step failed.</summary>
    public Dictionary<string, string> Outputs { get; set; } = [];

    /// <summary>
    /// Outputs that contain a secret or a masked value. Later steps can use them, but they don't leave the job: the
    /// job runner doesn't report them.
    /// </summary>
    public List<string> WithheldOutputs { get; set; } = [];

    /// <summary>Values the step asked to mask with <c>::add-mask::</c>, for the steps after it.</summary>
    public List<string> Masks { get; set; } = [];

    /// <summary>Why the step failed other than by its script's exit code, e.g. an invalid condition or a timeout.</summary>
    public string? Error { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StepPayload))]
[JsonSerializable(typeof(StepRunResult))]
public sealed partial class StepJsonContext : JsonSerializerContext
{
    public static StepPayload ReadPayload(string json) =>
        JsonSerializer.Deserialize(json, Default.StepPayload) ?? throw new JsonException("The step's payload is empty.");

    public static StepRunResult ReadResult(string json) =>
        JsonSerializer.Deserialize(json, Default.StepRunResult) ?? throw new JsonException("The step result is empty.");

    /// <summary>Reads a results file: one result per line.</summary>
    public static IReadOnlyList<StepRunResult> ReadResults(string jsonLines) =>
        jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ReadResult).ToArray();

    public static string Write(StepPayload payload) => JsonSerializer.Serialize(payload, Default.StepPayload);

    /// <summary>One line: results files hold one result per line.</summary>
    public static string Write(StepRunResult result) => JsonSerializer.Serialize(result, Default.StepRunResult);
}
