namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// Where a job's files live. The job runner keeps them in one directory on storage the job's containers also mount:
/// <code>
/// workspace/                   the shared workspace, at /octopus/workspace (OCTOPUS_WORKSPACE)
/// steps/&lt;index&gt;/input.json      the step's payload (OCTOPUS_INPUT), at /octopus/steps
/// steps/&lt;index&gt;/output          OCTOPUS_OUTPUT, written by the step
/// steps/&lt;index&gt;/result.json     OCTOPUS_RESULT, written by the step runner
/// steps/&lt;index&gt;/cancel          OCTOPUS_CANCEL, created by the job runner to stop the step
/// results/results.jsonl        OCTOPUS_RESULTS, one result per finished step; read-only at /octopus/results
/// </code>
/// Step runners are installed at <c>/octopus-step-runner/&lt;key&gt;/octopus-step-runner</c>, one per step runner image.
/// </summary>
public static class StepLayout
{
    public const string WorkspaceDirectory = "workspace";

    public const string StepsDirectory = "steps";

    public const string ResultsDirectory = "results";

    public const string ResultsFile = "results.jsonl";

    public const string InputFile = "input.json";

    public const string OutputFile = "output";

    public const string ResultFile = "result.json";

    public const string CancelFile = "cancel";

    public const string WorkspacePath = "/octopus/workspace";

    public const string StepsPath = "/octopus/steps";

    public const string ResultsPath = "/octopus/results";

    /// <summary>Where step runners are installed in a job's containers, read-only.</summary>
    public const string StepRunnersPath = "/octopus-step-runner";

    public const string StepRunnerFileName = "octopus-step-runner";

    /// <summary>A step's directory, relative to the job's.</summary>
    public static string StepDirectory(int index) => $"{StepsDirectory}/{index}";

    /// <summary>The step runner installed from the job's <paramref name="key"/>th step runner image.</summary>
    public static string StepRunnerPath(int key) => $"{StepRunnersPath}/{key}/{StepRunnerFileName}";

    /// <summary>The command that runs step <paramref name="index"/> in a job's container.</summary>
    public static IReadOnlyList<string> RunCommand(int index, int stepRunnerKey, string jobStatus)
    {
        var step = $"{StepsPath}/{index}";
        return
        [
            StepRunnerPath(stepRunnerKey),
            "run",
            $"{StepEnvironment.Input}={step}/{InputFile}",
            $"{StepEnvironment.Output}={step}/{OutputFile}",
            $"{StepEnvironment.Result}={step}/{ResultFile}",
            $"{StepEnvironment.Cancel}={step}/{CancelFile}",
            $"{StepEnvironment.Results}={ResultsPath}/{ResultsFile}",
            $"{StepEnvironment.Workspace}={WorkspacePath}",
            $"{StepEnvironment.JobStatus}={jobStatus}",
        ];
    }

    /// <summary>The command that keeps a job's container alive between steps: the step runner, idle.</summary>
    public static IReadOnlyList<string> IdleCommand(int stepRunnerKey) => [StepRunnerPath(stepRunnerKey), "idle"];
}
