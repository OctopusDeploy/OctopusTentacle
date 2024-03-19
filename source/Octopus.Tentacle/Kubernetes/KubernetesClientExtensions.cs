using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s.Autorest;

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
                url += $"&sinceTime={Uri.EscapeDataString(sinceTimeStr)}";
            }

            url = string.Concat(client.BaseUri, url);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

            if (client.Credentials is not null)
            {
                await client.Credentials.ProcessHttpRequestAsync(httpRequest, CancellationToken.None);
            }

            var httpResponse = await client.HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (httpResponse.IsSuccessStatusCode)
                return await httpResponse.Content.ReadAsStreamAsync();

            // an exception occurred, throw
            var responseContent = httpResponse.Content != null
                ? await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false)
                : string.Empty;

            var ex = new HttpOperationException($"Operation returned an invalid status code '{httpResponse.StatusCode}', response body {responseContent}")
            {
                Request = new HttpRequestMessageWrapper(httpRequest, null),
                Response = new HttpResponseMessageWrapper(httpResponse, responseContent)
            };

            httpRequest.Dispose();
            httpResponse.Dispose();

            throw ex;
        }
    }
}