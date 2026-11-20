using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Logs.Http;

/// <summary>
/// Streams a job's log to a generic HTTPS log endpoint inside the job's directory. The directory URI comes from
/// the Server; its query string (for example a token) is sent as given.
/// </summary>
/// <remarks>
/// The protocol:
/// <list type="bullet">
/// <item>Create: <c>PUT &lt;dir&gt;/&lt;file&gt;</c>, empty body, <c>If-None-Match: *</c>. 201 (or another 2xx) creates it;
/// 412 means it already exists, and a job's log is never overwritten.</item>
/// <item>Append: <c>POST &lt;dir&gt;/&lt;file&gt;</c>, the bytes as <c>application/octet-stream</c>, with
/// <c>Octopus-Append-Offset</c> = the bytes already written. The endpoint appends only when its length equals the
/// offset and answers 2xx with <c>Octopus-Log-Length</c>. On a mismatch it answers 412 with its current length,
/// which tells the runner how much of a retried chunk already landed.</item>
/// <item>401, 403 and 404 are permanent and fail the job. Anything else (5xx, timeouts) is transient: the append is
/// retried on the next flush.</item>
/// </list>
/// </remarks>
/// <param name="handler">The HTTP handler. Tests pass a fake endpoint; null uses a default handler.</param>
public sealed class HttpLogStore(HttpMessageHandler? handler = null) : ILogStore, IDisposable
{
    public const string AppendOffsetHeader = "Octopus-Append-Offset";
    public const string LogLengthHeader = "Octopus-Log-Length";

    const int MaxChunkBytes = 4 * 1024 * 1024;
    const int CreateAttempts = 3;
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    readonly HttpClient http = new(handler ?? new SocketsHttpHandler(), disposeHandler: handler is null) { Timeout = RequestTimeout };

    /// <summary>Delay between create attempts. Tests shorten it.</summary>
    internal Func<int, CancellationToken, Task> CreateRetryDelay { get; init; } =
        (attempt, ct) => Task.Delay(TimeSpan.FromSeconds(attempt), ct);

    public async Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
    {
        var uri = JobDirectory.FileUri(jobDirectory, fileName);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = new ByteArrayContent([]) };
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return new Appender(http, uri);
                }

                if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    throw new LogStorageException($"The log at {Redacted(uri)} already exists; a job's log is never overwritten.", permanent: true);
                }

                ThrowIfPermanent(response);
                throw new LogStorageException($"Log endpoint answered {(int)response.StatusCode} creating {Redacted(uri)}.", permanent: false);
            }
            catch (Exception ex) when (attempt < CreateAttempts && IsTransient(ex, ct))
            {
                await CreateRetryDelay(attempt, ct);
            }
        }
    }

    public void Dispose() => http.Dispose();

    static bool IsTransient(Exception ex, CancellationToken ct) =>
        ex switch
        {
            LogStorageException storage => !storage.Permanent,
            OperationCanceledException => !ct.IsCancellationRequested, // an HttpClient timeout, not the caller's cancellation
            HttpRequestException => true,
            _ => false,
        };

    static void ThrowIfPermanent(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            throw new LogStorageException($"Log endpoint refused the request ({(int)response.StatusCode} {response.ReasonPhrase}).", permanent: true);
        }
    }

    // The query may carry a token: keep it out of messages.
    static string Redacted(Uri uri) => uri.GetLeftPart(UriPartial.Path);

    static long? ReportedLength(HttpResponseMessage response) =>
        response.Headers.TryGetValues(LogLengthHeader, out var values)
        && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            ? length
            : null;

    sealed class Appender(HttpClient http, Uri uri) : ILogAppender
    {
        public long Length { get; private set; }

        public async Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            while (!data.IsEmpty)
            {
                var chunk = data[..Math.Min(data.Length, MaxChunkBytes)];
                using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new ReadOnlyMemoryContent(chunk) };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Headers.Add(AppendOffsetHeader, Length.ToString(CultureInfo.InvariantCulture));

                // A transport failure or timeout propagates as transient: StreamingLogWriter keeps the bytes and
                // retries them from this same offset on its next flush.
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    Length += chunk.Length;
                    data = data[chunk.Length..];
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    // Our offset didn't match. This appender is the log's only writer, so the endpoint can only be
                    // ahead of us because an earlier attempt landed after its response was lost. Count those bytes
                    // as written and carry on from the endpoint's length.
                    var reported = ReportedLength(response);
                    // Equal lengths with a 412 would be a protocol error, and would retry the same chunk forever.
                    if (reported is not { } length || length <= Length || length > Length + chunk.Length)
                    {
                        throw new LogStorageException(
                            $"Log endpoint's length ({reported?.ToString(CultureInfo.InvariantCulture) ?? "missing"}) doesn't match this runner's {Length} bytes plus a {chunk.Length}-byte append at {Redacted(uri)}.",
                            permanent: true);
                    }

                    var landed = (int)(length - Length);
                    Length = length;
                    data = data[landed..];
                    continue;
                }

                ThrowIfPermanent(response);
                throw new LogStorageException($"Log endpoint answered {(int)response.StatusCode} appending to {Redacted(uri)}.", permanent: false);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
