using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// Copied as is from the octopus server repo.
    /// </summary>
    public class OctopusPackageDownloader
    {
        public static async Task DownloadPackage(string downloadUrl, string filePath, ILogger logger)
        {
            string expectedHash = null;
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(150);
                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalLength = response.Content.Headers.ContentLength;
                    if (response.Headers.TryGetValues("x-amz-meta-sha256", out var expectedHashs))
                    {
                        expectedHash = expectedHashs.FirstOrDefault();
                    }

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
                        var totalRead = 0L;
                        var buffer = new byte[8192];

                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                        while (read != 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);

                            if (totalLength.HasValue && sw.ElapsedMilliseconds >= TimeSpan.FromSeconds(7).TotalMilliseconds)
                            {
                                var percentRead = totalRead * 1.0 / totalLength.Value * 100;
                                logger.Information($"Downloading Completed {percentRead}%");
                                sw.Reset();
                                sw.Start();
                            }

                            read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            totalRead += read;
                        }

                        logger.Information("Download Finished");
                    }
                }
            }

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