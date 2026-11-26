using System.Text;

namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// Replaces secret values in log lines with <c>***</c>, as GitHub Actions does: each value, each line of a multi-line
/// value, and their base64 and JSON-escaped forms. Every value is masked, however short. Masking is best effort: a
/// script can always print a secret in a form nobody thought of. Thread safe.
/// </summary>
public sealed class SecretMasker
{
    public const string Mask = "***";

    /// <summary>The GitHub Actions workflow command a step prints to add a mask, followed by the value.</summary>
    public const string AddMaskCommand = "::add-mask::";

    readonly Lock gate = new();
    readonly HashSet<string> values = new(StringComparer.Ordinal);
    string[] longestFirst = [];

    public void Add(string secret)
    {
        if (secret.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            var changed = false;
            foreach (var form in Forms(secret))
            {
                changed |= form.Length > 0 && values.Add(form);
            }

            if (changed)
            {
                longestFirst = values.OrderByDescending(v => v.Length).ToArray();
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (gate)
            {
                return values.Count == 0;
            }
        }
    }

    /// <summary>True when <paramref name="text"/> contains a masked value.</summary>
    public bool Contains(string text)
    {
        string[] masks;
        lock (gate)
        {
            masks = longestFirst;
        }

        return masks.Any(mask => text.Contains(mask, StringComparison.Ordinal));
    }

    public string Apply(string text)
    {
        string[] masks;
        lock (gate)
        {
            masks = longestFirst;
        }

        foreach (var mask in masks)
        {
            if (text.Contains(mask, StringComparison.Ordinal))
            {
                text = text.Replace(mask, Mask, StringComparison.Ordinal);
            }
        }

        return text;
    }

    static IEnumerable<string> Forms(string secret)
    {
        yield return secret;
        foreach (var line in secret.Split('\n'))
        {
            yield return line.TrimEnd('\r');
        }

        yield return Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));
        yield return JsonEscape(secret);
    }

    static string JsonEscape(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            escaped.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }

        return escaped.ToString();
    }
}
