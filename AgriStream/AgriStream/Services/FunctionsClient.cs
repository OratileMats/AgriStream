using System.Net.Http.Json;
using System.Text.Json;
using AgriStream.Models;

namespace AgriStream.Services
{
    public interface IFunctionsClient
    {
        Task<List<TelemetryDto>> GetLatestTelemetryAsync(int top, CancellationToken ct = default);
        Task<string?> TriggerIrrigationAsync(IrrigationRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Calls the HTTP-triggered Azure Functions (task d).
    ///   GET  {base}/telemetry?top=N   -> latest telemetry from Table Storage
    ///   POST {base}/irrigation        -> places an irrigation command on the Storage Queue
    /// </summary>
    public class FunctionsClient : IFunctionsClient
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
        private readonly HttpClient _http;

        public FunctionsClient(HttpClient http, IConfiguration config)
        {
            _http = http;
            var baseUrl = (config["Functions:BaseUrl"] ?? "http://localhost:7071/api").TrimEnd('/') + "/";
            _http.BaseAddress = new Uri(baseUrl);
            _http.Timeout = TimeSpan.FromSeconds(15);

            var key = config["Functions:Key"];
            if (!string.IsNullOrWhiteSpace(key))
                _http.DefaultRequestHeaders.Add("x-functions-key", key);
        }

        public async Task<List<TelemetryDto>> GetLatestTelemetryAsync(int top, CancellationToken ct = default)
        {
            var data = await _http.GetFromJsonAsync<List<TelemetryDto>>($"telemetry?top={top}", Json, ct);
            return data ?? new List<TelemetryDto>();
        }

        public async Task<string?> TriggerIrrigationAsync(IrrigationRequest request, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("irrigation", request, ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Name.Equals("commandId", StringComparison.OrdinalIgnoreCase))
                    return p.Value.GetString();
            return null;
        }
    }
}
