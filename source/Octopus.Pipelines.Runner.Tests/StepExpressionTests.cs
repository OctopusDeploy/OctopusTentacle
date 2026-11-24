using System.Diagnostics;
using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class StepExpressionTests
{
    static StepsContext Context()
    {
        var context = new StepsContext();
        context.Add(new StepResult("generate", StepOutcome.Success, 0, new Dictionary<string, string> { ["version"] = "1.4.0", ["Empty"] = "" }));
        context.Add(new StepResult("decide", StepOutcome.Success, 0, new Dictionary<string, string> { ["deploy"] = "true" }));
        return context;
    }

    [Theory]
    [InlineData("steps.decide.outputs.deploy == 'true'", true)]
    [InlineData("steps.decide.outputs.deploy == 'TRUE'", true)]
    [InlineData("steps.decide.outputs.deploy != 'true'", false)]
    [InlineData("${{ steps.decide.outputs.deploy == 'true' }}", true)]
    [InlineData("steps.generate.outputs.missing == ''", true)]
    [InlineData("steps.nope.outputs.x", false)]
    [InlineData("steps.generate.outputs.version", true)]
    [InlineData("steps.generate.outcome == 'success'", true)]
    [InlineData("!(steps.generate.outputs.empty)", true)]
    [InlineData("startsWith(steps.generate.outputs.version, '1.') && contains('abc', 'B')", true)]
    [InlineData("steps.decide.outputs.deploy == 'false' || steps.generate.outputs.version == '1.4.0'", true)]
    [InlineData("failure()", false)]
    [InlineData("always()", true)]
    [InlineData(null, true)]
    public void Evaluates_conditions(string? condition, bool expected) =>
        Assert.Equal(expected, ExprEvaluator.IsTrue(Expr.ParseCondition(condition), Context()));

    [Fact]
    public void A_condition_without_a_status_function_only_runs_after_success()
    {
        var context = Context();
        context.Add(new StepResult("broken", StepOutcome.Failure, 1, new Dictionary<string, string>()));

        Assert.False(ExprEvaluator.IsTrue(Expr.ParseCondition("steps.decide.outputs.deploy == 'true'"), context));
        Assert.True(ExprEvaluator.IsTrue(Expr.ParseCondition("always() && steps.decide.outputs.deploy == 'true'"), context));
        Assert.True(ExprEvaluator.IsTrue(Expr.ParseCondition("failure()"), context));
    }

    [Fact]
    public void Interpolates_expressions_into_text() =>
        Assert.Equal(
            "v=1.4.0 deploy=true missing= lit='}}'",
            ExprEvaluator.Interpolate("v=${{ steps.generate.outputs.version }} deploy=${{steps.decide.outputs.deploy}} missing=${{ steps.x.outputs.y }} lit=${{ '''}}''' }}", Context()));

    [Theory]
    [InlineData("steps.")]
    [InlineData("'unterminated")]
    [InlineData("a == ")]
    [InlineData("(a")]
    public void Rejects_malformed_expressions(string expression) =>
        Assert.Throws<FormatException>(() => Expr.Parse(expression));

    [Fact]
    public void Parses_output_files()
    {
        var outputs = StepOutputFile.Parse("version=1.4.0\nequation=a=b\nnotes<<EOF\nline 1\nline=2\nEOF\nversion=1.5.0\n");

        Assert.Equal("1.5.0", outputs["version"]);
        Assert.Equal("a=b", outputs["equation"]);
        Assert.Equal("line 1\nline=2", outputs["NOTES"]);
    }

    [Fact]
    public void Rejects_an_unclosed_multiline_output() =>
        Assert.Throws<FormatException>(() => StepOutputFile.Parse("notes<<EOF\nline 1\n"));

    [Theory]
    [InlineData("steps.decide.outputs.deploy == 'true'", true)]
    [InlineData("steps.decide.outputs.DEPLOY == 'True'", true)]
    [InlineData("steps.decide.outputs.deploy != 'true'", false)]
    [InlineData("steps.generate.outputs.notes == 'line 1\nline=2'", true)]
    [InlineData("steps.generate.outputs.missing", false)]
    [InlineData("steps.generate.outcome == 'success' && !failure()", true)]
    [InlineData("startsWith(steps.generate.outputs.version, '1.') || false", true)]
    [InlineData("endsWith(steps.generate.outputs.version, '.1')", false)]
    [InlineData(null, true)]
    public void Compiled_shell_conditions_agree_with_the_evaluator(string? condition, bool expected)
    {
        using var state = ShellState.Create();
        var compiler = new ShellExprCompiler(state.Root, ["generate", "decide"]);

        var (exitCode, _) = state.Run(compiler, $"if {compiler.Condition(Expr.ParseCondition(condition))}; then exit 0; else exit 1; fi");

        Assert.Equal(expected, exitCode == 0);
    }

    [Fact]
    public void Compiled_shell_success_turns_false_once_a_step_has_failed()
    {
        using var state = ShellState.Create();
        File.WriteAllText(Path.Combine(state.Root, "failed"), "");
        var compiler = new ShellExprCompiler(state.Root, ["generate", "decide"]);

        Assert.Equal(1, state.Run(compiler, $"{compiler.Condition(Expr.ParseCondition("steps.decide.outputs.deploy == 'true'"))}").ExitCode);
        Assert.Equal(0, state.Run(compiler, $"{compiler.Condition(Expr.ParseCondition("failure()"))}").ExitCode);
    }

    [Fact]
    public void Compiled_shell_words_keep_values_intact()
    {
        using var state = ShellState.Create();
        var compiler = new ShellExprCompiler(state.Root, ["generate", "decide"]);

        var (_, stdout) = state.Run(compiler, $"printf '%s|' {compiler.Word("v=${{ steps.generate.outputs.version }} $HOME `x` \"q\"")} {compiler.Word("${{ steps.generate.outputs.notes }}")}");

        Assert.Equal("v=1.4.0 $HOME `x` \"q\"|line 1\nline=2|", stdout);
    }

    /// <summary>A state directory with the outputs of two finished steps, and a way to run compiled shell against it.</summary>
    sealed class ShellState : IDisposable
    {
        ShellState(string root) => Root = root;

        public string Root { get; }

        public static ShellState Create()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Needs a POSIX shell.");
            var root = Directory.CreateTempSubdirectory("steps-").FullName;
            Directory.CreateDirectory(Path.Combine(root, "steps", "0"));
            Directory.CreateDirectory(Path.Combine(root, "steps", "1"));
            File.WriteAllText(Path.Combine(root, "steps", "0", "output"), "version=1.4.0\nnotes<<EOF\nline 1\nline=2\nEOF\n");
            File.WriteAllText(Path.Combine(root, "steps", "0", "outcome"), "success\n");
            File.WriteAllText(Path.Combine(root, "steps", "1", "output"), "deploy=true\n");
            File.WriteAllText(Path.Combine(root, "steps", "1", "outcome"), "success\n");
            return new ShellState(root);
        }

        public (int ExitCode, string Stdout) Run(ShellExprCompiler compiler, string script)
        {
            var path = Path.Combine(Root, "test.sh");
            File.WriteAllText(path, compiler.Library + script + "\n");
            using var process = Process.Start(new ProcessStartInfo("sh", [path]) { RedirectStandardOutput = true })!;
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
