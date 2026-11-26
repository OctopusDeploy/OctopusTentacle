using System.Globalization;
using Octopus.Pipelines.Runner.Core.Steps;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Octopus.Pipelines.Runner.Workflows;

/// <summary>One job of a workflow file, with each step's image resolved.</summary>
public sealed record WorkflowJob(
    string Id,
    IReadOnlyList<StepSpec> Steps,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyList<ServiceSpec> Services);

/// <summary>
/// Reads one job from a GitHub Actions workflow file: its <c>container</c>, <c>env</c>, <c>services</c> and
/// <c>run</c> steps (<c>id</c>, <c>name</c>, <c>if</c>, <c>env</c>, <c>shell</c>, <c>timeout-minutes</c>). A step may
/// also name its own <c>container</c>, which GitHub Actions doesn't allow on a <c>run</c> step (its nearest
/// equivalent is a <c>uses: docker://</c> step); it overrides the job's. A step with neither runs in the runner's
/// default image. Everything else (<c>on</c>, <c>runs-on</c>, <c>uses</c>) is ignored.
/// </summary>
/// <remarks>Uses YamlDotNet's representation model, which needs no reflection, so callers stay Native AOT clean.</remarks>
public static class WorkflowFile
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

        var jobImage = Image(job);
        var steps = new List<StepSpec>();
        var stepNodes = job.Children.TryGetValue("steps", out var node) && node is YamlSequenceNode sequence
            ? sequence.Children
            : throw new FormatException($"Job {id} has no steps.");
        foreach (var stepNode in stepNodes.Cast<YamlMappingNode>())
        {
            var stepId = Scalar(stepNode, "id") ?? $"step{steps.Count + 1}";
            var run = Scalar(stepNode, "run") ?? throw new FormatException($"Step {stepId} has no run: only run steps are supported.");
            steps.Add(new StepSpec(stepId, run)
            {
                Name = Scalar(stepNode, "name"),
                If = Scalar(stepNode, "if"),
                Image = Image(stepNode) ?? jobImage,
                Shell = Scalar(stepNode, "shell"),
                Timeout = Scalar(stepNode, "timeout-minutes") is { } minutes
                    ? TimeSpan.FromMinutes(double.Parse(minutes, CultureInfo.InvariantCulture))
                    : null,
                Env = Env(stepNode),
            });
        }

        var duplicate = steps.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new FormatException($"Job {id} has more than one step '{duplicate.Key}'.");
        }

        var services = Mapping(job, "services")?.Children
            .Select(s => new ServiceSpec(Text(s.Key), Image(s.Value) ?? throw new FormatException($"Service {Text(s.Key)} needs an image."))
            {
                Env = s.Value is YamlMappingNode mapping ? Env(mapping) : new Dictionary<string, string>(),
            })
            .ToArray() ?? [];
        return new WorkflowJob(id, steps, Env(job), services);
    }

    /// <summary><c>container: image</c>, <c>container: { image: image }</c>, or a service's <c>image</c>.</summary>
    static string? Image(YamlNode node) => node switch
    {
        YamlScalarNode scalar => scalar.Value,
        YamlMappingNode mapping when mapping.Children.TryGetValue("container", out var container) => Image(container),
        YamlMappingNode mapping => Scalar(mapping, "image"),
        _ => null,
    };

    static Dictionary<string, string> Env(YamlMappingNode node) =>
        Mapping(node, "env")?.Children.ToDictionary(e => Text(e.Key), e => Text(e.Value)) ?? [];

    static YamlMappingNode? Mapping(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(key, out var value) ? value as YamlMappingNode : null;

    static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(key, out var value) ? Text(value) : null;

    static string Text(YamlNode node) =>
        node is YamlScalarNode { Value: { } value } ? value : throw new FormatException($"Expected a scalar at {node.Start}.");
}
