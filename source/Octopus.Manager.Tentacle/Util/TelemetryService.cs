using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Manager.Tentacle.Infrastructure;

namespace Octopus.Manager.Tentacle.Util
{
    public interface ITelemetryService
    {
        Task<bool> SendTelemetryEvent(Uri serverUri, TelemetryEvent eventObj, IWebProxy proxy);
    }

    public class TelemetryService : ITelemetryService
    {
        public async Task<bool> SendTelemetryEvent(Uri serverUri, TelemetryEvent eventObj, IWebProxy proxy = null)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                if (proxy != null)
                    httpClientHandler.Proxy = proxy;
                
                using (var httpClient = new HttpClient(httpClientHandler, true))
                {
                    httpClient.BaseAddress = serverUri;

                    var eventBatch = new TelemetryEventBatch(eventObj);
                    var stringPayload = JsonConvert.SerializeObject(eventBatch);

                    try
                    {
                        var response = await httpClient.PostAsync("api/telemetry/process", new StringContent(stringPayload, Encoding.UTF8, "application/json"));
                        return response.IsSuccessStatusCode;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }
    }
}
