using AgriStream.Data;
using AgriStream.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Services
{
    /// <summary>
    /// Stores a sensor reading in Table Storage. If the sensor reports a problem
    /// (very low soil moisture) it automatically places an irrigation command via the POST function.
    /// </summary>
    public class TelemetryIngestService
    {
        public const double LowMoistureThreshold = 25;

        private readonly TelemetryService _telemetry;
        private readonly IFunctionsClient _functions;
        private readonly AppDbContext _db;
        private readonly ILogger<TelemetryIngestService> _logger;

        public TelemetryIngestService(TelemetryService telemetry, IFunctionsClient functions,
            AppDbContext db, ILogger<TelemetryIngestService> logger)
        {
            _telemetry = telemetry;
            _functions = functions;
            _db = db;
            _logger = logger;
        }

        /// <returns>true if an automatic irrigation command was queued.</returns>
        public async Task<bool> ProcessAsync(TelemetryDto reading, CancellationToken ct = default)
        {
            var low = reading.SoilMoisture < LowMoistureThreshold;
            reading.Status = low ? "Attention" : "Active";
            await _telemetry.AddReadingAsync(reading);

            if (!low) return false;

            var device = await _db.Devices.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeviceId == reading.SensorId, ct);
            if (device == null || device.Status == "Irrigating") return false;

            try
            {
                await _functions.TriggerIrrigationAsync(new IrrigationRequest
                {
                    DeviceId = reading.SensorId,
                    Action = "Start",
                    RequestedBy = $"sensor:{reading.SensorId}",
                    Reason = $"Auto: soil moisture {reading.SoilMoisture:0.#}% below {LowMoistureThreshold}%"
                }, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not queue automatic irrigation for {Sensor}", reading.SensorId);
                return false;
            }
        }
    }
}
