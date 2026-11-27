using System.Globalization;
using System.Text.RegularExpressions;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Steps;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Octopus.Pipelines.Runner.Workflows;

/// <summary>A <c>run</c> step as written in a workflow file.</summary>
public sealed record WorkflowStep(string Id, string Run)
{
    public string? Name { get; init; }
    public string? If { get; init; }
    public string? Image { get; init; }
    public string? Shell { get; init; }
    public TimeSpan? Timeout { get; init; }
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
}

/// <summary>One job of a workflow file.</summary>
public sealed record WorkflowJob(
    string Id,
    string? Image,
    IReadOnlyList<WorkflowStep> Steps,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyList<ServiceSpec> Services)
{
    public IReadOnlyList<RegistryCredential> RegistryCredentials { get; init; } = [];

    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// The job's steps as the Server sends them: each step's version 1 payload holds its name, condition, shell, script,
    /// its env with the job's merged in, and every one of the job's <paramref name="secrets"/>.
    /// </summary>
    public IReadOnlyList<StepSpec> ToSteps(string stepRunnerImage, IReadOnlyDictionary<string, string> secrets) =>
        Steps.Select(step => new StepSpec(step.Id, stepRunnerImage, StepJsonContext.Write(new StepPayload
        {
            Id = step.Id,
            Name = step.Name,
            If = step.If,
            Shell = step.Shell,
            Env = Env.Concat(step.Env).GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.Last().Value),
            Secrets = secrets.ToDictionary(),
            Run = step.Run,
        }))
        {
            Image = step.Image,
            Timeout = step.Timeout,
        }).ToArray();
}

/// <summary>
/// Reads one job from a GitHub Actions workflow file: its <c>container</c> (with <c>credentials</c>), <c>env</c>,
/// <c>timeout-minutes</c>, <c>services</c> (with <c>env</c> and <c>options: --health-cmd</c>) and <c>run</c> steps
/// (<c>id</c>, <c>name</c>, <c>if</c>, <c>env</c>, <c>shell</c>, <c>timeout-minutes</c>). A step may also name its own
/// <c>container</c>, which GitHub Actions doesn't allow on a <c>run</c> step (its nearest equivalent is a
/// <c>uses: docker://</c> step). Everything else (<c>on</c>, <c>runs-on</c>, <c>uses</c>) is ignored.
/// </summary>
/// <remarks>Uses YamlDotNet's representation model, which needs no reflection, so callers stay Native AOT clean.</remarks>
public static partial class WorkflowFile
{
    public static WorkflowJob Load(string path, string? jobId = null)
    {
        var yaml = new YamlStream();
        using (var reader = File.OpenText(path))
        {
            try
            {
                yaml.Load(reader);
            }
            catch (YamlException e)
            {
                throw new FormatException($"{path} isn't valid YAML: {e.Message}", e);
            }
        }

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new FormatException($"{path} isn't a workflow: it has no top-level mapping.");
        }

        var jobs = Mapping(root, "jobs") ?? throw new FormatException($"{path} has no jobs.");
        var (id, job) = jobId is null
            ? jobs.Children.Count == 1
                ? (Text(jobs.Children.Keys.Single()), (YamlMappingNode)jobs.Children.Values.Single())
                : throw new FormatException($"{path} has {jobs.Children.Count} jobs; pick one.")
            : (jobId, Mapping(jobs, jobId) ?? throw new FormatException($"{path} has no job '{jobId}'."));

        var steps = new List<WorkflowStep>();
        var stepNodes = job.Children.TryGetValue("steps", out var node) && node is YamlSequenceNode sequence
            ? sequence.Children
            : throw new FormatException($"Job {id} has no steps.");
        foreach (var stepNode in stepNodes.Cast<YamlMappingNode>())
        {
            var stepId = Scalar(stepNode, "id") ?? $"step{steps.Count + 1}";
            var run = Scalar(stepNode, "run") ?? throw new FormatException($"Step {stepId} has no run: only run steps are supported.");
            steps.Add(new WorkflowStep(stepId, run)
            {
                Name = Scalar(stepNode, "name"),
                If = Scalar(stepNode, "if"),
                Image = ContainerImage(stepNode),
                Shell = Scalar(stepNode, "shell"),
                Timeout = Minutes(stepNode),
                Env = Env(stepNode),
            });
        }

        var duplicate = steps.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new FormatException($"Job {id} has more than one step '{duplicate.Key}'.");
        }

        var services = Mapping(job, "services")?.Children
            .Select(s => Service(Text(s.Key), s.Value))
            .ToArray() ?? [];
        var image = ContainerImage(job);
        return new WorkflowJob(id, image, steps, Env(job), services)
        {
            RegistryCredentials = Credentials(job, image),
            Timeout = Minutes(job),
        };
    }

    static ServiceSpec Service(string name, YamlNode node)
    {
        if (node is YamlScalarNode { Value: { } image })
        {
            return new ServiceSpec(name, image);
        }

        var mapping = node as YamlMappingNode ?? throw new FormatException($"Service {name} must be an image or a mapping.");
        var health = Scalar(mapping, "options") is { } options && HealthCmd().Match(options) is { Success: true } match
            ? match.Groups["cmd"].Value
            : null;
        return new ServiceSpec(name, Scalar(mapping, "image") ?? throw new FormatException($"Service {name} needs an image."))
        {
            Env = Env(mapping),
            // As GitHub Actions' --health-cmd: a shell command, run until it succeeds.
            ReadinessCommand = health is null ? [] : ["sh", "-c", health],
        };
    }

    /// <summary><c>--health-cmd "redis-cli ping"</c>, <c>--health-cmd 'pg_isready'</c> or <c>--health-cmd pg_isready</c>.</summary>
    [GeneratedRegex("""--health-cmd[ =](?:"(?<cmd>[^"]*)"|'(?<cmd>[^']*)'|(?<cmd>\S+))""")]
    private static partial Regex HealthCmd();

    /// <summary><c>container: image</c> or <c>container: { image: image }</c>.</summary>
    static string? ContainerImage(YamlMappingNode node) => node.Children.TryGetValue("container", out var container)
        ? container switch
        {
            YamlScalarNode scalar => scalar.Value,
            YamlMappingNode mapping => Scalar(mapping, "image") ?? throw new FormatException("A container mapping needs an image."),
            _ => throw new FormatException("container must be an image name or a mapping with an image."),
        }
        : null;

    /// <summary>The job container's <c>credentials</c>, for the registry its image comes from.</summary>
    static IReadOnlyList<RegistryCredential> Credentials(YamlMappingNode job, string? image)
    {
        if (image is null || Mapping(job, "container") is not { } container || Mapping(container, "credentials") is not { } credentials)
        {
            return [];
        }

        var first = image.Split('/')[0];
        var registry = image.Contains('/') && (first.Contains('.') || first.Contains(':') || first == "localhost") ? first : "docker.io";
        return [new RegistryCredential(registry, Scalar(credentials, "username") ?? "", Scalar(credentials, "password") ?? "")];
    }

    static TimeSpan? Minutes(YamlMappingNode node) => Scalar(node, "timeout-minutes") is { } minutes
        ? TimeSpan.FromMinutes(double.Parse(minutes, CultureInfo.InvariantCulture))
        : null;

    static Dictionary<string, string> Env(YamlMappingNode node) =>
        Mapping(node, "env")?.Children.ToDictionary(e => Text(e.Key), e => Text(e.Value)) ?? [];

    static YamlMappingNode? Mapping(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(key, out var value) ? value as YamlMappingNode : null;

    static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(key, out var value) ? Text(value) : null;

    static string Text(YamlNode node) =>
        node is YamlScalarNode { Value: { } value } ? value : throw new FormatException($"Expected a scalar at {node.Start}.");
}
