using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// The environment variables the job runner gives the step runner, and the paths of the files a step uses.
/// </summary>
public static class StepEnvironment
{
    /// <summary>The job's shared workspace, and the step's default working directory.</summary>
    public const string Workspace = "OCTOPUS_WORKSPACE";

    /// <summary>The file a step writes <c>name=value</c> outputs to. Isolated between steps.</summary>
    public const string Output = "OCTOPUS_OUTPUT";

    /// <summary>The <see cref="StepInput"/> document. For the step runner only; scripts don't see it.</summary>
    public const string Input = "OCTOPUS_INPUT";

    /// <summary>Where the step runner writes its <see cref="StepRunResult"/>. For the step runner only; scripts don't see it.</summary>
    public const string Result = "OCTOPUS_RESULT";

    /// <summary>Set to the same paths as their OCTOPUS_ equivalents, so scripts written for GitHub Actions work.</summary>
    public const string GitHubWorkspace = "GITHUB_WORKSPACE";

    public const string GitHubOutput = "GITHUB_OUTPUT";

    /// <summary>The largest value one output may have, as in GitHub Actions.</summary>
    public const int MaxOutputBytes = 1024 * 1024;

    /// <summary>The most all of a job's outputs may add up to, as in GitHub Actions.</summary>
    public const long MaxJobOutputBytes = 50L * 1024 * 1024;
}

/// <summary>What the job runner gives the step runner: the step as written, and the context its expressions see.</summary>
public sealed class StepInput
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public StepJob Job { get; set; } = new();

    public StepDefinition Step { get; set; } = new();

    public StepContext Context { get; set; } = new();
}

/// <summary>Identifies the job, for logging. Expressions can't refer to it.</summary>
public sealed class StepJob
{
    public string Id { get; set; } = "";

    public string RunId { get; set; } = "";
}

/// <summary>A step as written in the workflow, with its expressions unresolved.</summary>
public sealed class StepDefinition
{
    public string Id { get; set; } = "";

    public string? Name { get; set; }

    public string? Image { get; set; }

    /// <summary>The step's condition, with or without <c>${{ }}</c>. Null means <c>success()</c>.</summary>
    public string? If { get; set; }

    /// <summary>How to run <see cref="Run"/>, e.g. <c>bash -e {0}</c>; <c>{0}</c> is the script's path. Null means the default.</summary>
    public string? Shell { get; set; }

    public Dictionary<string, string> Env { get; set; } = [];

    public string Run { get; set; } = "";
}

/// <summary>Everything a step's expressions can refer to. The job runner adds each finished step's result.</summary>
public sealed class StepContext
{
    public StepContextJob Job { get; set; } = new();

    /// <summary>The job's environment, as <c>env.&lt;name&gt;</c>. Steps get it as environment variables too.</summary>
    public Dictionary<string, string> Env { get; set; } = [];

    /// <summary>The steps that have finished, by id, as <c>steps.&lt;id&gt;</c>.</summary>
    public Dictionary<string, StepContextStep> Steps { get; set; } = [];

    /// <summary>The job's secrets, as <c>secrets.&lt;name&gt;</c>. Every value is masked in logs.</summary>
    public Dictionary<string, string> Secrets { get; set; } = [];

    /// <summary>Values earlier steps asked to mask, with <c>::add-mask::</c>.</summary>
    public List<string> Masks { get; set; } = [];
}

public sealed class StepContextJob
{
    /// <summary><c>success</c>, or <c>failure</c> once a step has failed.</summary>
    public string Status { get; set; } = JobStatus.Success;
}

public static class JobStatus
{
    public const string Success = "success";
    public const string Failure = "failure";
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
}

/// <summary>What the step runner reports back: how the step ended and what it set.</summary>
public sealed class StepRunResult
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string StepId { get; set; } = "";

    public StepRunOutcome Outcome { get; set; }

    /// <summary>The script's exit code, or null when it was skipped or never started.</summary>
    public int? ExitCode { get; set; }

    /// <summary>What the step wrote to <c>OCTOPUS_OUTPUT</c>, kept even when the step failed.</summary>
    public Dictionary<string, string> Outputs { get; set; } = [];

    /// <summary>Values the step asked to mask with <c>::add-mask::</c>, for the steps after it.</summary>
    public List<string> Masks { get; set; } = [];

    /// <summary>Why the step failed other than by its script's exit code, e.g. an invalid condition.</summary>
    public string? Error { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(StepInput))]
[JsonSerializable(typeof(StepRunResult))]
public sealed partial class StepJsonContext : JsonSerializerContext
{
    public static StepInput ReadInput(string json) =>
        JsonSerializer.Deserialize(json, Default.StepInput) ?? throw new JsonException("The step input is empty.");

    public static StepRunResult ReadResult(string json) =>
        JsonSerializer.Deserialize(json, Default.StepRunResult) ?? throw new JsonException("The step result is empty.");

    public static string Write(StepInput input) => JsonSerializer.Serialize(input, Default.StepInput);

    public static string Write(StepRunResult result) => JsonSerializer.Serialize(result, Default.StepRunResult);
}
