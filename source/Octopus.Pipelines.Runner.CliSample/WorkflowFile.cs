using Octopus.Pipelines.Runner.Core.Steps;
using YamlDotNet.RepresentationModel;

namespace Octopus.Pipelines.Runner.CliSample;

/// <summary>One job of a workflow file, with each step's image resolved.</summary>
sealed record WorkflowJob(string Id, IReadOnlyList<StepSpec> Steps);

/// <summary>
/// Reads one job from a GitHub Actions workflow file: its <c>container</c>, <c>env</c> and <c>run</c> steps (<c>id</c>,
/// <c>name</c>, <c>if</c>, <c>env</c>). A step may also name its own <c>container</c>, which GitHub Actions doesn't
/// allow on a <c>run</c> step (its nearest equivalent is a <c>uses: docker://</c> step); it overrides the job's. A step
/// with neither runs in the runner's default image. Everything else (<c>on</c>, <c>runs-on</c>, <c>uses</c>) is ignored.
/// </summary>
/// <remarks>Uses YamlDotNet's representation model, which needs no reflection, so the CLI stays Native AOT clean.</remarks>
static class WorkflowFile
{
    public static WorkflowJob Load(string path, string? jobId)
    {
        var yaml = new YamlStream();
        using (var reader = File.OpenText(path))
        {
            yaml.Load(reader);
        }

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new FormatException($"{path} isn't a workflow: it has no top-level mapping.");
        }

        var jobs = Mapping(root, "jobs") ?? throw new FormatException($"{path} has no jobs.");
        var (id, job) = jobId is null
            ? jobs.Children.Count == 1
                ? (Text(jobs.Children.Keys.Single()), (YamlMappingNode)jobs.Children.Values.Single())
                : throw new FormatException($"{path} has {jobs.Children.Count} jobs; pick one with --job.")
            : (jobId, Mapping(jobs, jobId) ?? throw new FormatException($"{path} has no job '{jobId}'."));

        var jobImage = Image(job);
        var jobEnv = Env(job);
        var steps = new List<StepSpec>();
        var stepNodes = job.Children.TryGetValue("steps", out var node) && node is YamlSequenceNode sequence
            ? sequence.Children
            : throw new FormatException($"Job {id} has no steps.");
        foreach (var stepNode in stepNodes.Cast<YamlMappingNode>())
        {
            var stepId = Scalar(stepNode, "id") ?? $"step{steps.Count + 1}";
            var run = Scalar(stepNode, "run") ?? throw new FormatException($"Step {stepId} has no run: only run steps are supported.");
            var env = new Dictionary<string, string>(jobEnv);
            foreach (var (name, value) in Env(stepNode))
            {
                env[name] = value;
            }

            steps.Add(new StepSpec(stepId, run)
            {
                Name = Scalar(stepNode, "name"),
                If = Scalar(stepNode, "if"),
                Image = Image(stepNode) ?? jobImage,
                Env = env,
            });
        }

        var duplicate = steps.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        return duplicate is null ? new WorkflowJob(id, steps) : throw new FormatException($"Job {id} has more than one step '{duplicate.Key}'.");
    }

    /// <summary><c>container: image</c> or <c>container: { image: image }</c>.</summary>
    static string? Image(YamlMappingNode node) =>
        node.Children.TryGetValue("container", out var container)
            ? container switch
            {
                YamlScalarNode scalar => scalar.Value,
                YamlMappingNode mapping => Scalar(mapping, "image") ?? throw new FormatException("A container mapping needs an image."),
                _ => throw new FormatException("container must be an image name or a mapping with an image."),
            }
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
