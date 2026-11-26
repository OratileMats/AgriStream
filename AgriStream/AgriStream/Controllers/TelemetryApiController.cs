using System.Security.Cryptography;
using System.Text;
using AgriStream.Models;
using AgriStream.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Controllers
{
    /// <summary>
    /// Ingestion endpoint used by field sensors (and the load-test script).
    /// Secured with an API key header instead of a user cookie.
    /// </summary>
    [ApiController]
    [Route("api/telemetry")]
    public class TelemetryApiController : ControllerBase
    {
        private readonly TelemetryIngestService _ingest;
        private readonly IConfiguration _config;

        public TelemetryApiController(TelemetryIngestService ingest, IConfiguration config)
        {
            _ingest = ingest;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Ingest([FromBody] TelemetryIngestRequest request,
            [FromHeader(Name = "X-Api-Key")] string? apiKey, CancellationToken ct)
        {
            var expected = _config["Ingest:ApiKey"];
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(apiKey) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(apiKey), Encoding.UTF8.GetBytes(expected)))
                return Unauthorized();

            var autoIrrigation = await _ingest.ProcessAsync(new TelemetryDto
            {
                SensorId = request.SensorId,
                FarmName = request.FarmName,
                SoilMoisture = request.SoilMoisture,
                TemperatureC = request.TemperatureC,
                Humidity = request.Humidity
            }, ct);

            return Accepted(new { stored = true, autoIrrigationQueued = autoIrrigation });
        }
    }
}
