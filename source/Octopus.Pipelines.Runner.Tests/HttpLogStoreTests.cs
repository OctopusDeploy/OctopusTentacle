using System.Globalization;
using System.Net;
using System.Text;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Logs.Http;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Runs the HTTP log store against an in-memory endpoint that implements the protocol, so these check what goes over the wire.</summary>
public sealed class HttpLogStoreTests
{
    static readonly Uri JobDirectory = new("https://server.example/logs/runs/r/j?token=abc");

    static HttpLogStore Store(FakeLogEndpoint endpoint) => new(endpoint) { CreateRetryDelay = (_, _) => Task.CompletedTask };

    [Fact]
    public async Task Creates_the_log_then_appends_at_the_current_offset()
    {
        var endpoint = new FakeLogEndpoint();
        var ct = TestContext.Current.CancellationToken;

        await using var appender = await Store(endpoint).CreateAsync(JobDirectory, "job.log", ct);
        await appender.AppendAsync(Encoding.UTF8.GetBytes("hello"), ct);
        await appender.AppendAsync(Encoding.UTF8.GetBytes(" world"), ct);

        Assert.Equal(["PUT", "POST", "POST"], endpoint.Requests.Select(r => r.Method));
        Assert.All(endpoint.Requests, r => Assert.Equal("https://server.example/logs/runs/r/j/job.log?token=abc", r.Uri));
        Assert.Equal("*", endpoint.Requests[0].IfNoneMatch);
        Assert.Equal(["0", "5"], endpoint.Requests.Skip(1).Select(r => r.Offset));
        Assert.All(endpoint.Requests.Skip(1), r => Assert.Equal("application/octet-stream", r.ContentType));
        Assert.Equal("hello world", endpoint.Text);
        Assert.Equal(11, appender.Length);
    }

    [Fact]
    public async Task Never_overwrites_an_existing_log()
    {
        var endpoint = new FakeLogEndpoint { Exists = true };

        var ex = await Assert.ThrowsAsync<LogStorageException>(() => Store(endpoint).CreateAsync(JobDirectory, "job.log", TestContext.Current.CancellationToken));

        Assert.True(ex.Permanent);
        Assert.Contains("already exists", ex.Message);
        Assert.DoesNotContain("token", ex.Message);
    }

    [Fact]
    public async Task Retries_a_transient_create_failure()
    {
        var endpoint = new FakeLogEndpoint { CreateFailures = 1 };

        await using var appender = await Store(endpoint).CreateAsync(JobDirectory, "job.log", TestContext.Current.CancellationToken);

        Assert.Equal(2, endpoint.Requests.Count(r => r.Method == "PUT"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Treats_a_refused_append_as_permanent(HttpStatusCode status)
    {
        var endpoint = new FakeLogEndpoint();
        var ct = TestContext.Current.CancellationToken;
        await using var appender = await Store(endpoint).CreateAsync(JobDirectory, "job.log", ct);
        endpoint.AppendStatus = status;

        var ex = await Assert.ThrowsAsync<LogStorageException>(() => appender.AppendAsync(new byte[] { 1 }, ct));

        Assert.True(ex.Permanent);
    }

    [Fact]
    public async Task Treats_a_server_error_as_transient_and_retries_from_the_same_offset()
    {
        var endpoint = new FakeLogEndpoint();
        var ct = TestContext.Current.CancellationToken;
        await using var appender = await Store(endpoint).CreateAsync(JobDirectory, "job.log", ct);
        endpoint.AppendStatus = HttpStatusCode.ServiceUnavailable;

        var ex = await Assert.ThrowsAsync<LogStorageException>(() => appender.AppendAsync(Encoding.UTF8.GetBytes("abc"), ct));
        Assert.False(ex.Permanent);
        Assert.Equal(0, appender.Length);

        endpoint.AppendStatus = null;
        await appender.AppendAsync(Encoding.UTF8.GetBytes("abc"), ct);

        Assert.Equal("abc", endpoint.Text);
        Assert.Equal(["0", "0"], endpoint.Requests.Skip(1).Select(r => r.Offset));
    }

    [Fact]
    public async Task Does_not_duplicate_an_append_whose_response_was_lost()
    {
        var endpoint = new FakeLogEndpoint();
        var ct = TestContext.Current.CancellationToken;
        await using var appender = await Store(endpoint).CreateAsync(JobDirectory, "job.log", ct);
        await appender.AppendAsync(Encoding.UTF8.GetBytes("one "), ct);
        endpoint.LoseNextAppendResponse = true;

        // The endpoint applies "two " but the runner sees a failure, so its writer retries those bytes with the next ones.
        await Assert.ThrowsAsync<LogStorageException>(() => appender.AppendAsync(Encoding.UTF8.GetBytes("two "), ct));
        await appender.AppendAsync(Encoding.UTF8.GetBytes("two three"), ct);

        Assert.Equal("one two three", endpoint.Text);
        Assert.Equal(13, appender.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("99")]
    public async Task Fails_permanently_when_the_endpoint_reports_an_impossible_length(string? reported)
    {
        var endpoint = new FakeLogEndpoint();
        var ct = TestContext.Current.CancellationToken;
        await using var appender = await Store(endpoint).CreateAsync(JobDirectory, "job.log", ct);
        await appender.AppendAsync(Encoding.UTF8.GetBytes("four"), ct);
        endpoint.ForceMismatchLength = reported ?? "";

        var ex = await Assert.ThrowsAsync<LogStorageException>(() => appender.AppendAsync(Encoding.UTF8.GetBytes("more"), ct));

        Assert.True(ex.Permanent);
    }

    sealed class FakeLogEndpoint : HttpMessageHandler
    {
        readonly MemoryStream log = new();

        public bool Exists { get; set; }
        public int CreateFailures { get; set; }
        public HttpStatusCode? AppendStatus { get; set; }
        public bool LoseNextAppendResponse { get; set; }

        /// <summary>When set, every append answers 412 with this length ("" = no length header).</summary>
        public string? ForceMismatchLength { get; set; }

        public List<Request> Requests { get; } = [];

        public string Text => Encoding.UTF8.GetString(log.ToArray());

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new Request(
                request.Method.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.IfNoneMatch.FirstOrDefault()?.Tag,
                request.Headers.TryGetValues(HttpLogStore.AppendOffsetHeader, out var offsets) ? offsets.First() : null,
                request.Content?.Headers.ContentType?.MediaType));

            if (request.Method == HttpMethod.Put)
            {
                if (CreateFailures-- > 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                if (Exists)
                {
                    return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                }

                Exists = true;
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (AppendStatus is { } status)
            {
                return new HttpResponseMessage(status);
            }

            if (ForceMismatchLength is { } forced)
            {
                var mismatch = new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                if (forced != "")
                {
                    mismatch.Headers.Add(HttpLogStore.LogLengthHeader, forced);
                }

                return mismatch;
            }

            var offset = long.Parse(Requests[^1].Offset!, CultureInfo.InvariantCulture);
            if (offset != log.Length)
            {
                var mismatch = new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                mismatch.Headers.Add(HttpLogStore.LogLengthHeader, log.Length.ToString(CultureInfo.InvariantCulture));
                return mismatch;
            }

            log.Write(body);
            if (LoseNextAppendResponse)
            {
                LoseNextAppendResponse = false;
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            var ok = new HttpResponseMessage(HttpStatusCode.NoContent);
            ok.Headers.Add(HttpLogStore.LogLengthHeader, log.Length.ToString(CultureInfo.InvariantCulture));
            return ok;
        }

        public sealed record Request(string Method, string Uri, string? IfNoneMatch, string? Offset, string? ContentType);
    }
}
