using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class LogStoresTests
{
    [Fact]
    public void Adds_the_file_name_and_keeps_the_SAS()
    {
        var uri = JobDirectory.FileUri(new Uri("https://a.blob.core.windows.net/c-x/runs/r/j?sv=1&sig=a%2Bb"), JobFiles.Log);

        Assert.Equal("https://a.blob.core.windows.net/c-x/runs/r/j/job.log?sv=1&sig=a%2Bb", uri.AbsoluteUri);
    }

    [Fact]
    public async Task Routes_by_scheme_and_rejects_unknown_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        var files = new RecordingStore();
        var stores = new LogStores(new Dictionary<string, ILogStore> { ["file"] = files });
        var directory = new Uri("file:///tmp/c-x/runs/r/j");

        await stores.CreateAsync(directory, JobFiles.Log, ct);

        Assert.Equal((directory, JobFiles.Log), files.Created);
        await Assert.ThrowsAsync<NotSupportedException>(() => stores.CreateAsync(new Uri("ftp://x/y"), JobFiles.Log, ct));
    }

    sealed class RecordingStore : ILogStore
    {
        public (Uri Directory, string FileName)? Created { get; private set; }

        public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
        {
            Created = (jobDirectory, fileName);
            return Task.FromResult<ILogAppender>(null!);
        }
    }
}
