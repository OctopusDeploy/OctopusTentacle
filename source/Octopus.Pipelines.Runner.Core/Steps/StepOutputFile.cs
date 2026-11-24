namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>
/// Reads the <c>$GITHUB_OUTPUT</c> file format: <c>name=value</c> lines, and <c>name&lt;&lt;DELIMITER</c> ... <c>DELIMITER</c>
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

    /// <summary>
    /// The same parser as a POSIX shell function, <c>__out &lt;file&gt; &lt;name&gt;</c>, matching names case-insensitively, for
    /// orchestrating steps inside a container. Uses only <c>awk</c>, which busybox, Alpine and Debian images all have.
    /// </summary>
    public const string ShellFunction = """
        __out() {
          [ -f "$1" ] || return 0
          awk -v k="$2" '
            BEGIN { k = tolower(k) }
            d != "" { if ($0 == d) { if (tolower(n) == k) v = b; d = ""; next } b = (s ? b "\n" : "") $0; s = 1; next }
            { i = index($0, "<<"); j = index($0, "=")
              if (i > 1 && (j == 0 || i < j)) { n = substr($0, 1, i - 1); d = substr($0, i + 2); b = ""; s = 0; next }
              if (j > 1 && tolower(substr($0, 1, j - 1)) == k) v = substr($0, j + 1) }
            END { printf "%s", v }' "$1"
        }
        """;
}
