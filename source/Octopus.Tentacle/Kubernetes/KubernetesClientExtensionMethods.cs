using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s.Autorest;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesClientExtensionMethods
    {
        public static async Task<Stream> GetNamespacedPodLogsAsync(this k8s.Kubernetes client, string podName, string namespaceParameter, string container, DateTimeOffset? sinceTime, CancellationToken cancellationToken = default)
        {
            //Include Server timestamps so we can use it to specify "sinceTime" on the next call
            var url = $"api/v1/namespaces/{namespaceParameter}/pods/{podName}/log?container={container}&timestamps=true";

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