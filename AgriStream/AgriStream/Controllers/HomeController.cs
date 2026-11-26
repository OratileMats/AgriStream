using System.Diagnostics;
using AgriStream.Data;
using AgriStream.Models;
using AgriStream.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _db;
        private readonly IFunctionsClient _functions;
        private readonly TelemetryService _telemetry;

        public HomeController(ILogger<HomeController> logger, AppDbContext db,
            IFunctionsClient functions, TelemetryService telemetry)
        {
            _logger = logger;
            _db = db;
            _functions = functions;
            _telemetry = telemetry;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var vm = new DashboardViewModel
            {
                UserCount = await _db.Users.CountAsync(),
                DeviceCount = await _db.Devices.CountAsync(),
                IrrigatingCount = await _db.Devices.CountAsync(d => d.Status == "Irrigating")
            };

            try { vm.RecentAudit = await _telemetry.GetAuditAsync(5); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not load audit entries"); }

            return View(vm);
        }

        
        /// Dashboard. The MVC app calls the HTTP GET Azure Function,
        /// so the function key never reaches the browser.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Telemetry(CancellationToken ct)
        {
            try
            {
                return Json(await _functions.GetLatestTelemetryAsync(40, ct));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telemetry function call failed");
                return StatusCode(502, new { error = "Telemetry service unavailable" });
            }
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
