using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class StepExpressionTests
{
    static StepContext Context(string status = JobStatus.Success) => new()
    {
        Job = new StepContextJob { Status = status },
        Env = new Dictionary<string, string> { ["RELEASE_VERSION"] = "1.4.0" },
        Secrets = new Dictionary<string, string> { ["TOKEN"] = "s3cr3t" },
        Steps = new Dictionary<string, StepContextStep>
        {
            ["generate"] = new() { Outcome = StepRunOutcome.Success, Outputs = new Dictionary<string, string> { ["version"] = "1.4.0", ["Empty"] = "" } },
            ["decide"] = new() { Outcome = StepRunOutcome.Success, Outputs = new Dictionary<string, string> { ["deploy"] = "true" } },
        },
    };

    static bool IsTrue(string? condition, StepContext? context = null) =>
        ExprEvaluator.IsTrue(Expr.ParseCondition(condition), new ExpressionContext(context ?? Context()));

    [Theory]
    [InlineData("steps.decide.outputs.deploy == 'true'", true)]
    [InlineData("steps.DECIDE.outputs.DEPLOY == 'TRUE'", true)]
    [InlineData("steps.decide.outputs.deploy != 'true'", false)]
    [InlineData("${{ steps.decide.outputs.deploy == 'true' }}", true)]
    [InlineData("steps.generate.outputs.missing == ''", true)]
    [InlineData("steps.nope.outputs.x", false)]
    [InlineData("steps.generate.outputs.version", true)]
    [InlineData("steps.generate.outcome == 'success'", true)]
    [InlineData("!(steps.generate.outputs.empty)", true)]
    [InlineData("env.release_version == '1.4.0'", true)]
    [InlineData("secrets.TOKEN == 's3cr3t'", true)]
    [InlineData("job.status == 'success'", true)]
    [InlineData("startsWith(steps.generate.outputs.version, '1.') && contains('abc', 'B')", true)]
    [InlineData("steps.decide.outputs.deploy == 'false' || steps.generate.outputs.version == '1.4.0'", true)]
    [InlineData("'1' == 1.0", true)]
    [InlineData("failure()", false)]
    [InlineData("always()", true)]
    [InlineData(null, true)]
    public void Evaluates_conditions(string? condition, bool expected) => Assert.Equal(expected, IsTrue(condition));

    [Fact]
    public void A_condition_without_a_status_function_only_runs_while_the_job_is_succeeding()
    {
        var failed = Context(JobStatus.Failure);

        Assert.False(IsTrue("steps.decide.outputs.deploy == 'true'", failed));
        Assert.False(IsTrue(null, failed));
        Assert.True(IsTrue("always() && steps.decide.outputs.deploy == 'true'", failed));
        Assert.True(IsTrue("failure()", failed));
    }

    [Fact]
    public void Interpolates_expressions_into_text() =>
        Assert.Equal(
            "v=1.4.0 deploy=true missing= lit='}}'",
            ExprEvaluator.Interpolate(
                "v=${{ steps.generate.outputs.version }} deploy=${{steps.decide.outputs.deploy}} missing=${{ steps.x.outputs.y }} lit=${{ '''}}''' }}",
                new ExpressionContext(Context())));

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

    [Fact]
    public void Round_trips_the_step_documents()
    {
        var input = new StepInput { Step = new StepDefinition { Id = "deploy", If = "always()", Run = "echo hi" }, Context = Context() };
        var result = new StepRunResult { StepId = "deploy", Outcome = StepRunOutcome.Skipped };

        var json = StepJsonContext.Write(input);

        Assert.Contains("\"outcome\": \"success\"", json);
        Assert.Equal("deploy", StepJsonContext.ReadInput(json).Step.Id);
        Assert.Equal("1.4.0", StepJsonContext.ReadInput(json).Context.Steps["generate"].Outputs["version"]);
        Assert.Equal(StepRunOutcome.Skipped, StepJsonContext.ReadResult(StepJsonContext.Write(result)).Outcome);
        Assert.DoesNotContain("exitCode", StepJsonContext.Write(result));
    }

    [Fact]
    public void Masks_secrets_their_lines_and_their_encodings()
    {
        var masker = new SecretMasker();
        masker.Add("pa\"ss");
        masker.Add("line one\nline two");
        masker.Add("x");

        Assert.Equal("token=*** json=\"***\" b64=***", masker.Apply("token=pa\"ss json=\"pa\\\"ss\" b64=cGEic3M="));
        Assert.Equal("first: ***, second: ***", masker.Apply("first: line one, second: line two"));
        Assert.Equal("e***it", masker.Apply("exit"));
        Assert.True(masker.Contains("a line two b"));
        Assert.False(masker.Contains("nothing here"));
    }

    [Fact]
    public void Ignores_empty_secrets()
    {
        var masker = new SecretMasker();
        masker.Add("");

        Assert.True(masker.IsEmpty);
        Assert.Equal("text", masker.Apply("text"));
    }
}
