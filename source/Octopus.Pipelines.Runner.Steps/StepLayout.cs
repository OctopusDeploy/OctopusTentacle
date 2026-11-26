namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// Where a job's files live on the shared storage that the job runner and every step's container see. The job's
/// directory is mounted into each step's container at <see cref="MountPath"/>:
/// <code>
/// workspace/                  the shared workspace (OCTOPUS_WORKSPACE)
/// steps/&lt;index&gt;/input.json     OCTOPUS_INPUT, written by the job runner
/// steps/&lt;index&gt;/output         OCTOPUS_OUTPUT, written by the step
/// steps/&lt;index&gt;/result.json    OCTOPUS_RESULT, written by the step runner
/// </code>
/// </summary>
public static class StepLayout
{
    public const string MountPath = "/octopus";

    /// <summary>Where the step runner executable is mounted in a step's container, apart from the job's files.</summary>
    public const string StepRunnerDirectory = "/octopus-step-runner";

    public const string StepRunnerFileName = "octopus-step-runner";

    public const string StepRunnerPath = StepRunnerDirectory + "/" + StepRunnerFileName;

    public const string WorkspaceDirectory = "workspace";

    public const string InputFile = "input.json";

    public const string OutputFile = "output";

    public const string ResultFile = "result.json";

    /// <summary>A step's directory, relative to the job's.</summary>
    public static string StepDirectory(int index) => $"steps/{index}";

    /// <summary>The step runner's environment variables for step <paramref name="index"/>, as paths inside the container.</summary>
    public static IReadOnlyDictionary<string, string> Variables(int index) => new Dictionary<string, string>
    {
        [StepEnvironment.Workspace] = $"{MountPath}/{WorkspaceDirectory}",
        [StepEnvironment.Input] = $"{MountPath}/{StepDirectory(index)}/{InputFile}",
        [StepEnvironment.Output] = $"{MountPath}/{StepDirectory(index)}/{OutputFile}",
        [StepEnvironment.Result] = $"{MountPath}/{StepDirectory(index)}/{ResultFile}",
    };
}
