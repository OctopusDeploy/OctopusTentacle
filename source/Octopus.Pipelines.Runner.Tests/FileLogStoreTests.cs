using System.Text;
using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class FileLogStoreTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "tspoc-filelogstore-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Creates_the_file_inside_the_job_directory()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileLogStore(root);

        await using (var appender = await store.CreateAsync(new Uri($"file://{root}/c-x/runs/r/j"), JobFiles.Log, ct))
        {
            await appender.AppendAsync(Encoding.UTF8.GetBytes("hi"), ct);
            Assert.Equal(2, appender.Length);
        }

        Assert.Equal("hi", await File.ReadAllTextAsync(Path.Combine(root, "c-x/runs/r/j/job.log"), ct));
    }

    [Theory]
    [InlineData("{root}/../escape")]
    [InlineData("{root}/c-x/../../etc")]
    [InlineData("/etc")]
    public async Task Refuses_a_directory_outside_the_root(string directory)
    {
        var store = new FileLogStore(root);
        var uri = new Uri($"file://{directory.Replace("{root}", root)}");

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(uri, JobFiles.Log, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../job.log")]
    [InlineData("a/job.log")]
    [InlineData("a\\job.log")]
    public async Task Refuses_a_file_name_that_is_not_a_plain_name(string fileName)
    {
        var store = new FileLogStore(root);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(new Uri($"file://{root}/c-x/runs/r/j"), fileName, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Never_overwrites_an_existing_log()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileLogStore(root);
        var directory = new Uri($"file://{root}/c-x/runs/r/j");
        await (await store.CreateAsync(directory, JobFiles.Log, ct)).DisposeAsync();

        await Assert.ThrowsAsync<IOException>(() => store.CreateAsync(directory, JobFiles.Log, ct));
    }
}
