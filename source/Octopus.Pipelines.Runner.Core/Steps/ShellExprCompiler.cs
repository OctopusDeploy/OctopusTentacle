using System.Text;

namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>
/// Compiles expressions to POSIX shell, for orchestrating steps inside a container where no .NET runs. Step outputs are
/// read from <c>&lt;root&gt;/steps/&lt;index&gt;/output</c> at the moment the compiled code runs, so a step's condition and
/// inputs see the outputs of the steps before it. The shell functions it calls are in <see cref="Library"/>.
/// </summary>
/// <remarks>
/// Comparisons are case-insensitive string comparisons; unlike GitHub Actions, <c>'1' == 1.0</c> is false.
/// </remarks>
public sealed class ShellExprCompiler(string root, IReadOnlyList<string> stepIds)
{
    /// <summary>Shell functions the compiled code needs. Source it first.</summary>
    public string Library => $$"""
        __root='{{root}}'
        {{StepOutputFile.ShellFunction}}
        __outcome() { cat "$__root/steps/$1/outcome" 2>/dev/null; }
        __lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }
        __eq() { [ "$(__lower "$1")" = "$(__lower "$2")" ]; }
        __contains() { case "$(__lower "$1")" in *"$(__lower "$2")"*) return 0;; esac; return 1; }
        __startswith() { case "$(__lower "$1")" in "$(__lower "$2")"*) return 0;; esac; return 1; }
        __endswith() { case "$(__lower "$1")" in *"$(__lower "$2")") return 0;; esac; return 1; }
        __truthy() { [ -n "$1" ]; }
        __success() { [ ! -e "$__root/failed" ]; }

        """;

    /// <summary>A shell command list that succeeds when <paramref name="expr"/> is true.</summary>
    public string Condition(Expr expr) => expr switch
    {
        Expr.Literal l => ExprEvaluator.IsTrue(l, new StepsContext()) ? "true" : "false",
        Expr.Ref r => $"__truthy \"{Value(r)}\"",
        Expr.Not n => $"! {{ {Condition(n.Operand)}; }}",
        Expr.And a => $"{{ {Condition(a.Left)}; }} && {{ {Condition(a.Right)}; }}",
        Expr.Or o => $"{{ {Condition(o.Left)}; }} || {{ {Condition(o.Right)}; }}",
        Expr.Compare c => $"{(c.Equal ? "" : "! ")}__eq \"{Value(c.Left)}\" \"{Value(c.Right)}\"",
        Expr.Call { Name: "success" } => "__success",
        Expr.Call { Name: "failure" } => "! __success",
        Expr.Call { Name: "always" } => "true",
        Expr.Call { Name: "cancelled" } => "false",
        Expr.Call { Name: "contains" or "startswith" or "endswith", Args.Count: 2 } c =>
            $"__{c.Name} \"{Value(c.Args[0])}\" \"{Value(c.Args[1])}\"",
        _ => throw new NotSupportedException($"Can't compile '{expr}' to shell."),
    };

    /// <summary>Text that expands to the expression's value inside a double-quoted shell string.</summary>
    public string Value(Expr expr)
    {
        switch (expr)
        {
            case Expr.Literal l:
                return EscapeForDoubleQuotes(ExprEvaluator.ToText(l.Value));
            case Expr.Ref r when r.Path.Count >= 3 && r.Path[0].Equals("steps", StringComparison.OrdinalIgnoreCase):
                var index = IndexOf(r.Path[1]);
                if (index < 0)
                {
                    return "";
                }

                return r.Path[2].ToLowerInvariant() switch
                {
                    "outputs" when r.Path.Count == 4 => $"$(__out \"$__root/steps/{index}/output\" '{r.Path[3].ToLowerInvariant()}')",
                    "outcome" or "conclusion" when r.Path.Count == 3 => $"$(__outcome {index})",
                    _ => "",
                };
            case Expr.Ref:
                return "";
            default:
                return $"$(if {Condition(expr)}; then echo true; else echo false; fi)";
        }
    }

    /// <summary>A script with each <c>${{ }}</c> replaced by shell that reads its value when the script runs.</summary>
    public string Script(string template) => ExprTemplate.Render(template, literal => literal, Value);

    /// <summary>A double-quoted shell word for a value that may hold <c>${{ }}</c> expressions.</summary>
    public string Word(string template) => $"\"{ExprTemplate.Render(template, EscapeForDoubleQuotes, Value)}\"";

    int IndexOf(string stepId)
    {
        for (var i = 0; i < stepIds.Count; i++)
        {
            if (stepIds[i].Equals(stepId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    static string EscapeForDoubleQuotes(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '"' or '$' or '`')
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
