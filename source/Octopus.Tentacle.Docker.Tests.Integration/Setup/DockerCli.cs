using System.Text;
using CliWrap;
using CliWrap.Buffered;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>A thin wrapper over the docker CLI, which is what CI agents and developer machines reliably have.</summary>
public static class DockerCli
{
    /// <summary>Applied to every container the tests start, so leftovers from aborted runs can be found and removed.</summary>
    public const string ContainerLabel = "octopus.tentacle.docker-tests=true";

    public static async Task<string> Run(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var args = arguments.ToArray();
        var result = await Cli.Wrap("docker")
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(Encoding.UTF8, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"`docker {string.Join(' ', args.Select(Redact))}` exited with code {result.ExitCode}.{Environment.NewLine}" +
                $"stdout: {result.StandardOutput.Trim()}{Environment.NewLine}" +
                $"stderr: {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Trim();
    }

    /// <summary>
    /// `docker logs` writes the container's stdout and stderr to its own stdout and stderr, so ask for both and
    /// return them together. Never throws: this is called while reporting other failures.
    /// </summary>
    public static async Task<string> Logs(string container)
    {
        try
        {
            var result = await Cli.Wrap("docker")
                .WithArguments(new[] { "logs", "--timestamps", container })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(Encoding.UTF8, CancellationToken.None);
            return result.StandardOutput + result.StandardError;
        }
        catch (Exception e)
        {
            return $"<could not read logs for {container}: {e.Message}>";
        }
    }

    static readonly string[] SecretVariables = { "ServerApiKey", "BearerToken", "ServerPassword" };

    static string Redact(string argument)
    {
        var secret = SecretVariables.FirstOrDefault(v => argument.StartsWith(v + "=", StringComparison.Ordinal));
        return secret is null ? argument : $"{secret}=<redacted>";
    }
}
