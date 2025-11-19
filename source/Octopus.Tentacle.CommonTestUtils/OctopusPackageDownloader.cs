using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.CommonTestUtils
{
    /// <summary>
    /// Copied as is from the octopus server repo.
    /// </summary>
    public class OctopusPackageDownloader
    {
        public static async Task DownloadPackage(string downloadUrl, string filePath, ILogger logger, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await AttemptToDownloadPackage(downloadUrl, filePath, logger, cancellationToken);
                    return;
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            throw new AggregateException(exceptions);
        }
        static async Task AttemptToDownloadPackage(string downloadUrl, string filePath, ILogger logger, CancellationToken cancellationToken)
        {
            var totalTime = Stopwatch.StartNew();
            var totalRead = 0L;
            string? expectedHash = null;
            try
            {
                using (var client = new HttpClient())
                {
                    // This appears to be the time it takes to do a single read/write, not the entire download.
                    client.Timeout = TimeSpan.FromSeconds(20);
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalLength = response.Content.Headers.ContentLength;
                        expectedHash = TryGetExpectedHashFromHeaders(response, expectedHash);

                        logger.Information($"Downloading {downloadUrl} ({totalLength} bytes)");

                        var sw = new Stopwatch();
                        sw.Start();
                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                               fileStream = new FileStream(
                                   filePath,
                                   FileMode.Create,
                                   FileAccess.Write,
                                   FileShare.None,
                                   8192,
                                   true))
                        {

                            var buffer = new byte[8192*4];
                            var singleReadTimeout = TimeSpan.FromSeconds(60); // 60s to read 32k
                            
                            var read = await ReadToBufferWithTimeout(cancellationToken, singleReadTimeout, contentStream, buffer);

                            while (read != 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);

                                if (totalLength.HasValue && sw.ElapsedMilliseconds >= TimeSpan.FromSeconds(7).TotalMilliseconds)
                                {
                                    var percentRead = totalRead * 1.0 / totalLength.Value * 100;
                                    logger.Information($"Downloading Completed {percentRead}%");
                                    sw.Reset();
                                    sw.Start();
                                }

                                read = await ReadToBufferWithTimeout(cancellationToken, singleReadTimeout, contentStream, buffer);
                                totalRead += read;
                            }
                            
                            totalTime.Stop();

                            logger.Information("Download Finished in {totalTime}ms", totalTime.ElapsedMilliseconds);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failure to download: {downloadUrl}. After {totalTime.Elapsed.TotalSeconds} seconds we only downloaded, {totalRead}", e);
            }

            ValidateDownload(filePath, expectedHash);
        }

        static async Task<int> ReadToBufferWithTimeout(CancellationToken cancellationToken, TimeSpan singleReadTimeout, Stream contentStream, byte[] buffer)
        {
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts.CancelAfter(singleReadTimeout); 
            var readTask = contentStream.ReadAsync(buffer, 0, buffer.Length, readCts.Token);
            // Don't trust that cancellation tokens will work on all dotnet versions or OSs we have to test in, so also add Task.Delay ;(
            await Task.WhenAny(Task.Delay(singleReadTimeout, cancellationToken), readTask);
            if (!readTask.IsCompleted) throw new TimeoutException();
            var read = await readTask;
            return read;
        }

        static string? TryGetExpectedHashFromHeaders(HttpResponseMessage response, string? expectedHash)
        {
            if (response.Headers.TryGetValues("x-amz-meta-sha256", out var expectedHashs))
            {
                expectedHash = expectedHashs.FirstOrDefault();
            }

            return expectedHash;
        }

        static void ValidateDownload(string filePath, string? expectedHash)
        {
            if (!expectedHash.IsNullOrEmpty())
            {
                using (var sha256 = SHA256.Create())
                {
                    var fileBytes = File.ReadAllBytes(filePath);
                    var hash = sha256.ComputeHash(fileBytes);
                    var computedHash = BitConverter.ToString(hash).Replace("-", "");
                    if (!computedHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Computed SHA256 ({computedHash}) hash of file does not match expected ({expectedHash})." + $"Downloaded file may be corrupt. File size {((long)fileBytes.Length)}");
                    }
                }
            }
        }
    }
}