namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// Reads the <c>$OCTOPUS_OUTPUT</c> file format (the same as GitHub Actions' <c>$GITHUB_OUTPUT</c>): <c>name=value</c> lines, and <c>name&lt;&lt;DELIMITER</c> ... <c>DELIMITER</c>
/// blocks for multi-line values. A name written twice keeps its last value.
/// </summary>
public static class StepOutputFile
{
    public static IReadOnlyDictionary<string, string> Parse(string content)
    {
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = content.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var heredoc = line.IndexOf("<<", StringComparison.Ordinal);
            var equals = line.IndexOf('=');
            if (heredoc > 0 && (equals < 0 || heredoc < equals))
            {
                var name = line[..heredoc];
                var delimiter = line[(heredoc + 2)..];
                var value = new List<string>();
                for (i++; i < lines.Length && lines[i] != delimiter; i++)
                {
                    value.Add(lines[i]);
                }

                if (i >= lines.Length)
                {
                    throw new FormatException($"Output '{name}' has no closing '{delimiter}'.");
                }

                outputs[name] = string.Join('\n', value);
            }
            else if (equals > 0)
            {
                outputs[line[..equals]] = line[(equals + 1)..];
            }
            else if (line.Length > 0)
            {
                throw new FormatException($"Invalid output line '{line}': expected name=value or name<<DELIMITER.");
            }
        }

        return outputs;
    }
}
