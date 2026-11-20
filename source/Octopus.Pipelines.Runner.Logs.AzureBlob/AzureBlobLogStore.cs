using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Logs.AzureBlob;

/// <summary>
/// Streams a job's log into an append blob inside the job's directory, authenticating with the SAS
/// carried in the directory URI. The SAS allows create and append on that directory and nothing else.
/// </summary>
/// <remarks>
/// Each flush is one Append Block request (up to 4 MiB), conditional on the append position, so a
/// retry of a block that already landed is detected instead of duplicated. 403 and 404 (an expired or
/// wrong SAS, a missing container) can't be fixed by retrying and surface as permanent failures.
/// </remarks>
/// <param name="options">Client options. Tests use it to swap in a fake transport.</param>
public sealed class AzureBlobLogStore(BlobClientOptions? options = null) : ILogStore
{
    const int MaxBlockBytes = 4 * 1024 * 1024;

    public async Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
    {
        var blob = new AppendBlobClient(JobDirectory.FileUri(jobDirectory, fileName), options);
        try
        {
            await blob.CreateAsync(new AppendBlobCreateOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain; charset=utf-8" },
                // Create-only: a job's log is never overwritten (its SAS has no overwrite permission either).
                Conditions = new AppendBlobRequestConditions { IfNoneMatch = ETag.All },
            }, ct);
        }
        catch (RequestFailedException ex) when (IsPermanent(ex))
        {
            throw Permanent(ex);
        }

        return new Appender(blob);
    }

    static bool IsPermanent(RequestFailedException ex) => ex.Status is 403 or 404;

    static LogStorageException Permanent(RequestFailedException ex) =>
        new($"Log storage refused the request ({ex.Status} {ex.ErrorCode}).", permanent: true, ex);

    sealed class Appender(AppendBlobClient blob) : ILogAppender
    {
        // Bytes from a failed append whose outcome is unknown: the request may have landed even though
        // the response was lost. StreamingLogWriter retries them at the front of its next append.
        long unconfirmed;

        public long Length { get; private set; }

        public async Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            while (!data.IsEmpty)
            {
                var chunk = data[..Math.Min(data.Length, MaxBlockBytes)];
                try
                {
                    using var content = new MemoryStream(chunk.ToArray(), writable: false);
                    await blob.AppendBlockAsync(content, new AppendBlobAppendBlockOptions
                    {
                        Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = Length },
                    }, ct);
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.AppendPositionConditionNotMet)
                {
                    // This appender is the blob's only writer (the job's SAS covers just this blob), so the
                    // position only moves under us when an earlier attempt landed after its response was
                    // lost: either the client's own retry of this block, or the writer's retry of a failed
                    // append. Count those bytes as written and carry on from there.
                    var landed = unconfirmed > 0 ? Math.Min(unconfirmed, chunk.Length) : chunk.Length;
                    unconfirmed = 0;
                    Length += landed;
                    data = data[(int)landed..];
                    continue;
                }
                catch (RequestFailedException ex) when (IsPermanent(ex))
                {
                    throw Permanent(ex);
                }
                catch
                {
                    unconfirmed = chunk.Length;
                    throw;
                }

                unconfirmed = 0;
                Length += chunk.Length;
                data = data[chunk.Length..];
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
