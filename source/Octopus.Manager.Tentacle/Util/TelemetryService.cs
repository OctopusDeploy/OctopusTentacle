using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Manager.Tentacle.Infrastructure;

namespace Octopus.Manager.Tentacle.Util
{
    public interface ITelemetryService
    {
        Task<bool> SendTelemetryEvent(Uri serverUri, TelemetryEvent eventObj);
    }
    
    public class TelemetryService: ITelemetryService
    {
        public async Task<bool> SendTelemetryEvent(Uri serverUri, TelemetryEvent eventObj)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = serverUri;

                var eventBatch = new TelemetryEventBatch(eventObj);
                var stringPayload = JsonConvert.SerializeObject(eventBatch);

                var response = await httpClient.PostAsync("api/telemetry/process", new StringContent(stringPayload, Encoding.UTF8, "application/json"));
                return response.IsSuccessStatusCode;
            }
        }
    }
}
