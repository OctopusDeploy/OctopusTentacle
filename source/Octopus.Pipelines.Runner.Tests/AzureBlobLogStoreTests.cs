using System.Net;
using System.Text;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Logs.AzureBlob;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Runs the real Blob SDK against a fake endpoint, so these check what goes over the wire.</summary>
public sealed class AzureBlobLogStoreTests
{
    static readonly Uri JobDirectory = new("https://acct.blob.core.windows.net/c-x/runs/r/j?sv=1&sig=abc");

    [Fact]
    public async Task Writes_to_the_file_inside_the_job_directory_with_the_SAS()
    {
        var blob = new FakeBlobEndpoint();
        var ct = TestContext.Current.CancellationToken;

        await using var appender = await Store(blob).CreateAsync(JobDirectory, "job.log", ct);
        await appender.AppendAsync(Encoding.UTF8.GetBytes("hello"), ct);

        Assert.Equal(2, blob.Requests.Count);
        Assert.Equal("https://acct.blob.core.windows.net/c-x/runs/r/j/job.log?sv=1&sig=abc", blob.Requests[0].Uri);
        Assert.Equal("*", blob.Requests[0].IfNoneMatch);
        Assert.Equal("https://acct.blob.core.windows.net/c-x/runs/r/j/job.log?sv=1&sig=abc&comp=appendblock", blob.Requests[1].Uri);
        Assert.Equal(5, appender.Length);
    }

    [Fact]
    public async Task Treats_an_expired_SAS_as_permanent()
    {
        var blob = new FakeBlobEndpoint { AppendStatus = HttpStatusCode.Forbidden, AppendErrorCode = "AuthenticationFailed" };
        var ct = TestContext.Current.CancellationToken;
        await using var appender = await Store(blob).CreateAsync(JobDirectory, "job.log", ct);

        var ex = await Assert.ThrowsAsync<LogStorageException>(() => appender.AppendAsync(new byte[] { 1 }, ct));

        Assert.True(ex.Permanent);
    }

    [Fact]
    public async Task Treats_a_forbidden_create_as_permanent()
    {
        var blob = new FakeBlobEndpoint { CreateStatus = HttpStatusCode.Forbidden, CreateErrorCode = "AuthorizationFailure" };

        var ex = await Assert.ThrowsAsync<LogStorageException>(() => Store(blob).CreateAsync(JobDirectory, "job.log", TestContext.Current.CancellationToken));

        Assert.True(ex.Permanent);
    }

    [Fact]
    public async Task Counts_a_landed_retry_as_written()
    {
        var blob = new FakeBlobEndpoint { AppendStatus = HttpStatusCode.PreconditionFailed, AppendErrorCode = "AppendPositionConditionNotMet" };
        var ct = TestContext.Current.CancellationToken;
        await using var appender = await Store(blob).CreateAsync(JobDirectory, "job.log", ct);

        await appender.AppendAsync(new byte[7], ct);

        Assert.Equal(7, appender.Length);
    }

    static AzureBlobLogStore Store(FakeBlobEndpoint blob) =>
        new(new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(blob)),
            Retry = { MaxRetries = 0 },
        });

    sealed record SentRequest(string Uri, string? IfNoneMatch);

    sealed class FakeBlobEndpoint : HttpMessageHandler
    {
        public HttpStatusCode CreateStatus { get; init; } = HttpStatusCode.Created;

        public string? CreateErrorCode { get; init; }

        public HttpStatusCode AppendStatus { get; init; } = HttpStatusCode.Created;

        public string? AppendErrorCode { get; init; }

        public List<SentRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ifNoneMatch = request.Headers.IfNoneMatch.SingleOrDefault()?.ToString();
            Requests.Add(new SentRequest(request.RequestUri!.AbsoluteUri, ifNoneMatch));

            var append = request.RequestUri!.Query.Contains("comp=appendblock", StringComparison.Ordinal);
            var (status, code) = append ? (AppendStatus, AppendErrorCode) : (CreateStatus, CreateErrorCode);
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
            if (code is not null)
            {
                response.Headers.Add("x-ms-error-code", code);
            }
            else
            {
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"0x1\"");
                response.Content.Headers.LastModified = DateTimeOffset.UtcNow;
                response.Headers.Add("x-ms-blob-append-offset", "0");
                response.Headers.Add("x-ms-blob-committed-block-count", "1");
            }

            return Task.FromResult(response);
        }
    }
}
