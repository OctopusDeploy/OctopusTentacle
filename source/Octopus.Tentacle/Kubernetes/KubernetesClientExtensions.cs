using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesClientExtensions
    {
        public static async Task<Stream> GetNamespacedPodLogsAsync(this k8s.Kubernetes client, string podName, string namespaceParameter, string container, DateTimeOffset? sinceTime = null, CancellationToken cancellationToken = default)
        {
            var url = $"api/v1/namespaces/{namespaceParameter}/pods/{podName}/log?container={container}";

            if (sinceTime is not null)
            {
                var sinceTimeStr = sinceTime.Value.ToString("O");
                url += $"sinceTime={Uri.EscapeDataString(sinceTimeStr)}";
            }

            url = string.Concat(client.BaseUri, url);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

            if (client.Credentials is not null)
            {
                await client.Credentials.ProcessHttpRequestAsync(httpRequest, CancellationToken.None);
            }

            var response = await client.HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            return await response.Content.ReadAsStreamAsync();
        }
    }
}