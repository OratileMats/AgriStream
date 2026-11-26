using AgriStream.Data;
using AgriStream.Models;
using AgriStream.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Controllers
{
    [Authorize]
    public class DevicesController : Controller
    {
        private static readonly Random Rng = new();
        private readonly AppDbContext _db;
        private readonly IFunctionsClient _functions;
        private readonly TelemetryIngestService _ingest;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(AppDbContext db, IFunctionsClient functions,
            TelemetryIngestService ingest, ILogger<DevicesController> logger)
        {
            _db = db;
            _functions = functions;
            _ingest = ingest;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
            => View(await _db.Devices.AsNoTracking().OrderBy(d => d.FarmName).ThenBy(d => d.DeviceId).ToListAsync());

        /// <summary>"Trigger Irrigation" button -> HTTP POST function -> Storage Queue.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Irrigate(string deviceId, string action = "Start")
        {
            if (action != "Start" && action != "Stop") return BadRequest();

            try
            {
                var id = await _functions.TriggerIrrigationAsync(new IrrigationRequest
                {
                    DeviceId = deviceId,
                    Action = action,
                    RequestedBy = User.Identity?.Name ?? "unknown",
                    Reason = "Manual command from dashboard"
                });
                TempData["Message"] = $"{action} command for {deviceId} queued (id {id}). Status updates once the queue function processes it.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Irrigation command failed");
                TempData["Error"] = "Could not reach the irrigation function. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Generates one telemetry reading per device (acts as the "sensor stream" for demos).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = $"{Roles.Admin},{Roles.Agronomist}")]
        public async Task<IActionResult> Simulate()
        {
            var devices = await _db.Devices.AsNoTracking().ToListAsync();
            var auto = 0;
            foreach (var d in devices)
            {
                var queued = await _ingest.ProcessAsync(new TelemetryDto
                {
                    SensorId = d.DeviceId,
                    FarmName = d.FarmName,
                    SoilMoisture = Math.Round(Rng.NextDouble() * 60 + 20, 1),
                    TemperatureC = Math.Round(Rng.NextDouble() * 15 + 18, 1),
                    Humidity = Math.Round(Rng.NextDouble() * 40 + 40, 1)
                });
                if (queued) auto++;
            }
            TempData["Message"] = $"Simulated {devices.Count} sensor readings. {auto} automatic irrigation command(s) queued.";
            return RedirectToAction(nameof(Index));
        }
    }
}
