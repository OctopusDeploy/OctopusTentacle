using System.Globalization;
using System.Text;

namespace Octopus.Pipelines.Runner.Steps;

/// <summary>
/// A subset of GitHub Actions expressions: string, number and boolean literals, the <c>steps</c>, <c>job</c>,
/// <c>env</c> and <c>secrets</c> contexts (see <see cref="ExpressionContext"/>), <c>== != &amp;&amp; || !</c>, parentheses, and the
/// <c>success() failure() always() cancelled() contains() startsWith() endsWith()</c> functions.
/// </summary>
public abstract record Expr
{
    public static Expr Parse(string text) => new ExprParser(text).ParseAll();

    /// <summary>
    /// A step's <c>if:</c>. As in GitHub Actions the <c>${{ }}</c> wrapper is optional, a missing condition means
    /// <c>success()</c>, and one that calls no status function is treated as <c>success() &amp;&amp; (condition)</c>.
    /// </summary>
    public static Expr ParseCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return new Call("success", []);
        }

        var text = condition.Trim();
        if (text.StartsWith("${{", StringComparison.Ordinal) && text.EndsWith("}}", StringComparison.Ordinal))
        {
            text = text[3..^2];
        }

        var expr = Parse(text);
        return expr.CallsStatusFunction() ? expr : new And(new Call("success", []), expr);
    }

    bool CallsStatusFunction() => this switch
    {
        Call c when c.Name is "success" or "failure" or "always" or "cancelled" => true,
        Call c => c.Args.Any(a => a.CallsStatusFunction()),
        Not n => n.Operand.CallsStatusFunction(),
        And a => a.Left.CallsStatusFunction() || a.Right.CallsStatusFunction(),
        Or o => o.Left.CallsStatusFunction() || o.Right.CallsStatusFunction(),
        Compare c => c.Left.CallsStatusFunction() || c.Right.CallsStatusFunction(),
        _ => false,
    };

    public sealed record Literal(object? Value) : Expr;

    /// <summary>A context property such as <c>steps.build.outputs.version</c>, split on dots.</summary>
    public sealed record Ref(IReadOnlyList<string> Path) : Expr
    {
        public override string ToString() => string.Join('.', Path);
    }

    public sealed record Not(Expr Operand) : Expr;

    public sealed record And(Expr Left, Expr Right) : Expr;

    public sealed record Or(Expr Left, Expr Right) : Expr;

    public sealed record Compare(Expr Left, Expr Right, bool Equal) : Expr;

    public sealed record Call(string Name, IReadOnlyList<Expr> Args) : Expr;
}

/// <summary>
/// What an expression can see, from a <see cref="StepContext"/>: <c>steps.&lt;id&gt;.outputs.&lt;name&gt;</c>,
/// <c>steps.&lt;id&gt;.outcome</c> and <c>.conclusion</c>, <c>job.status</c>, <c>env.&lt;name&gt;</c> and
/// <c>secrets.&lt;name&gt;</c>. Names are case-insensitive, as in GitHub Actions; anything else is null.
/// </summary>
public sealed class ExpressionContext(StepContext context)
{
    readonly Dictionary<string, StepContextStep> steps = new(context.Steps, StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> env = new(context.Env, StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> secrets = new(context.Secrets, StringComparer.OrdinalIgnoreCase);

    /// <summary>As GitHub Actions' <c>failure()</c>: an earlier step failed.</summary>
    public bool AnyStepFailed => context.Job.Status == JobStatus.Failure
        || context.Steps.Values.Any(s => s.Outcome == StepRunOutcome.Failure);

    /// <summary>As GitHub Actions' <c>cancelled()</c>: the job was cancelled.</summary>
    public bool Cancelled => context.Job.Status == JobStatus.Cancelled;

    public object? Lookup(IReadOnlyList<string> path)
    {
        if (path.Count < 2)
        {
            return null;
        }

        return path[0].ToLowerInvariant() switch
        {
            "steps" when path.Count >= 3 && steps.TryGetValue(path[1], out var step) => path[2].ToLowerInvariant() switch
            {
                "outputs" when path.Count == 4 => step.Outputs.FirstOrDefault(o => o.Key.Equals(path[3], StringComparison.OrdinalIgnoreCase)).Value,
                "outcome" or "conclusion" when path.Count == 3 => step.Outcome.ToString().ToLowerInvariant(),
                _ => null,
            },
            "job" when path.Count == 2 && path[1].Equals("status", StringComparison.OrdinalIgnoreCase) => context.Job.Status,
            "env" when path.Count == 2 => env.GetValueOrDefault(path[1]),
            "secrets" when path.Count == 2 => secrets.GetValueOrDefault(path[1]),
            _ => null,
        };
    }
}

/// <summary>Evaluates expressions on the orchestrator, the way the GitHub Actions runner does.</summary>
public static class ExprEvaluator
{
    public static bool IsTrue(Expr expr, ExpressionContext context) => Truthy(Evaluate(expr, context));

    public static object? Evaluate(Expr expr, ExpressionContext context) => expr switch
    {
        Expr.Literal l => l.Value,
        Expr.Ref r => context.Lookup(r.Path),
        Expr.Not n => !Truthy(Evaluate(n.Operand, context)),
        // && and || return an operand, not a boolean, as in GitHub Actions.
        Expr.And a => Evaluate(a.Left, context) is var left && !Truthy(left) ? left : Evaluate(a.Right, context),
        Expr.Or o => Evaluate(o.Left, context) is var left && Truthy(left) ? left : Evaluate(o.Right, context),
        Expr.Compare c => LooseEquals(Evaluate(c.Left, context), Evaluate(c.Right, context)) == c.Equal,
        Expr.Call c => Call(c, context),
        _ => throw new NotSupportedException(expr.GetType().Name),
    };

    /// <summary>Replaces each <c>${{ }}</c> in <paramref name="template"/> with its value.</summary>
    public static string Interpolate(string template, ExpressionContext context) =>
        ExprTemplate.Render(template, literal => literal, expr => ToText(Evaluate(expr, context)));

    public static string ToText(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        double d => d.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    static object? Call(Expr.Call call, ExpressionContext context)
    {
        string Arg(int i) => ToText(Evaluate(call.Args[i], context));
        return call.Name switch
        {
            "success" => !context.AnyStepFailed && !context.Cancelled,
            "failure" => context.AnyStepFailed,
            "always" => true,
            "cancelled" => context.Cancelled,
            "contains" => Arg(0).Contains(Arg(1), StringComparison.OrdinalIgnoreCase),
            "startswith" => Arg(0).StartsWith(Arg(1), StringComparison.OrdinalIgnoreCase),
            "endswith" => Arg(0).EndsWith(Arg(1), StringComparison.OrdinalIgnoreCase),
            _ => throw new NotSupportedException($"Unknown function '{call.Name}()'."),
        };
    }

    static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        double d => d != 0 && !double.IsNaN(d),
        string s => s.Length > 0,
        _ => true,
    };

    /// <summary>Strings compare ignoring case; mixed types compare as numbers, as in GitHub Actions.</summary>
    static bool LooseEquals(object? left, object? right)
    {
        if (left is string ls && right is string rs)
        {
            return string.Equals(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        if (left is null && right is null)
        {
            return true;
        }

        var ln = ToNumber(left);
        var rn = ToNumber(right);
        return !double.IsNaN(ln) && ln == rn;
    }

    static double ToNumber(object? value) => value switch
    {
        null => 0,
        bool b => b ? 1 : 0,
        double d => d,
        string { Length: 0 } => 0,
        string s => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN,
        _ => double.NaN,
    };
}

/// <summary>Splits text into literal runs and <c>${{ }}</c> expressions.</summary>
public static class ExprTemplate
{
    public static string Render(string template, Func<string, string> literal, Func<Expr, string> expression)
    {
        var result = new StringBuilder();
        var position = 0;
        while (true)
        {
            var start = template.IndexOf("${{", position, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(literal(template[position..]));
                return result.ToString();
            }

            result.Append(literal(template[position..start]));
            var end = FindEnd(template, start + 3);
            result.Append(expression(Expr.Parse(template[(start + 3)..end])));
            position = end + 2;
        }
    }

    /// <summary>The index of the <c>}}</c> that closes an expression, skipping any inside string literals.</summary>
    static int FindEnd(string template, int from)
    {
        var quoted = false;
        for (var i = from; i < template.Length - 1; i++)
        {
            if (template[i] == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && template[i] == '}' && template[i + 1] == '}')
            {
                return i;
            }
        }

        throw new FormatException($"Unclosed '${{{{' in '{template}'.");
    }
}

sealed class ExprParser(string text)
{
    int position;

    public Expr ParseAll()
    {
        var expr = ParseOr();
        SkipSpace();
        if (position < text.Length)
        {
            throw Error($"unexpected '{text[position..]}'");
        }

        return expr;
    }

    Expr ParseOr()
    {
        var left = ParseAnd();
        while (Accept("||"))
        {
            left = new Expr.Or(left, ParseAnd());
        }

        return left;
    }

    Expr ParseAnd()
    {
        var left = ParseCompare();
        while (Accept("&&"))
        {
            left = new Expr.And(left, ParseCompare());
        }

        return left;
    }

    Expr ParseCompare()
    {
        var left = ParseUnary();
        while (true)
        {
            if (Accept("=="))
            {
                left = new Expr.Compare(left, ParseUnary(), Equal: true);
            }
            else if (Accept("!="))
            {
                left = new Expr.Compare(left, ParseUnary(), Equal: false);
            }
            else
            {
                return left;
            }
        }
    }

    Expr ParseUnary()
    {
        SkipSpace();
        if (position < text.Length && text[position] == '!' && !Peek("!="))
        {
            position++;
            return new Expr.Not(ParseUnary());
        }

        return ParsePrimary();
    }

    Expr ParsePrimary()
    {
        SkipSpace();
        if (position >= text.Length)
        {
            throw Error("unexpected end of expression");
        }

        var c = text[position];
        if (c == '(')
        {
            position++;
            var inner = ParseOr();
            Expect(")");
            return inner;
        }

        if (c == '\'')
        {
            return new Expr.Literal(ReadString());
        }

        if (char.IsDigit(c) || (c == '-' && position + 1 < text.Length && char.IsDigit(text[position + 1])))
        {
            var start = position++;
            while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.'))
            {
                position++;
            }

            return new Expr.Literal(double.Parse(text[start..position], CultureInfo.InvariantCulture));
        }

        if (char.IsLetter(c) || c == '_')
        {
            var name = ReadIdentifier();
            SkipSpace();
            if (Accept("("))
            {
                var args = new List<Expr>();
                if (!Accept(")"))
                {
                    do
                    {
                        args.Add(ParseOr());
                    }
                    while (Accept(","));

                    Expect(")");
                }

                return new Expr.Call(name.ToLowerInvariant(), args);
            }

            switch (name.ToLowerInvariant())
            {
                case "true":
                    return new Expr.Literal(true);
                case "false":
                    return new Expr.Literal(false);
                case "null":
                    return new Expr.Literal(null);
            }

            var path = new List<string> { name };
            while (position < text.Length && text[position] == '.')
            {
                position++;
                path.Add(ReadIdentifier());
            }

            return new Expr.Ref(path);
        }

        throw Error($"unexpected '{c}'");
    }

    string ReadIdentifier()
    {
        var start = position;
        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] is '_' or '-'))
        {
            position++;
        }

        return position > start ? text[start..position] : throw Error("expected a name");
    }

    string ReadString()
    {
        var value = new StringBuilder();
        position++;
        while (true)
        {
            if (position >= text.Length)
            {
                throw Error("unterminated string");
            }

            if (text[position] == '\'')
            {
                // '' is an escaped quote.
                if (position + 1 < text.Length && text[position + 1] == '\'')
                {
                    value.Append('\'');
                    position += 2;
                    continue;
                }

                position++;
                return value.ToString();
            }

            value.Append(text[position++]);
        }
    }

    bool Peek(string token) => string.CompareOrdinal(text, position, token, 0, token.Length) == 0;

    bool Accept(string token)
    {
        SkipSpace();
        if (!Peek(token))
        {
            return false;
        }

        position += token.Length;
        return true;
    }

    void Expect(string token)
    {
        if (!Accept(token))
        {
            throw Error($"expected '{token}'");
        }
    }

    void SkipSpace()
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }

    FormatException Error(string message) => new($"Invalid expression '{text}': {message} at position {position}.");
}
