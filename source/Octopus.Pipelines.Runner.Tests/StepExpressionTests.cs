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
        var payload = new StepPayload { Id = "deploy", If = "always()", Run = "echo hi" };
        var results = new StepRunResult { Id = "deploy", Outcome = StepRunOutcome.Skipped, WithheldOutputs = ["token"] };

        var line = StepJsonContext.Write(results);

        Assert.Equal("deploy", StepJsonContext.ReadPayload(StepJsonContext.Write(payload)).Id);
        Assert.DoesNotContain('\n', line);
        Assert.Contains("\"outcome\":\"skipped\"", line);
        Assert.DoesNotContain("exitCode", line);
        Assert.Equal(["deploy", "deploy"], StepJsonContext.ReadResults(line + "\n" + line + "\n").Select(r => r.Id));
        Assert.Equal(["token"], StepJsonContext.ReadResult(line).WithheldOutputs);
    }

    [Fact]
    public void Treats_a_cancelled_job_as_neither_succeeding_nor_failing()
    {
        var cancelled = Context(JobStatus.Cancelled);

        Assert.True(IsTrue("cancelled()", cancelled));
        Assert.False(IsTrue("success()", cancelled));
        Assert.False(IsTrue("failure()", cancelled));
        Assert.False(IsTrue(null, cancelled));
        Assert.True(IsTrue("always()", cancelled));
        Assert.False(IsTrue("cancelled()"));
    }

    [Fact]
    public void Builds_the_context_from_the_payload_and_the_earlier_results()
    {
        var payload = new StepPayload { Env = new Dictionary<string, string> { ["A"] = "1" }, Secrets = new Dictionary<string, string> { ["S"] = "x" } };
        var context = StepContext.For(payload, [new StepRunResult { Id = "one", Outcome = StepRunOutcome.Failure, Masks = ["m"] }], JobStatus.Success);
        var expressions = new ExpressionContext(context);

        Assert.True(ExprEvaluator.IsTrue(Expr.Parse("failure() && env.A == '1' && secrets.S == 'x' && steps.one.outcome == 'failure'"), expressions));
        Assert.Equal(["m"], context.Masks);
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
