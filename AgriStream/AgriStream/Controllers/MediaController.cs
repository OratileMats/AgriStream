using AgriStream.Data;
using AgriStream.Models;
using AgriStream.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Controllers
{
    [Authorize]
    public class MediaController : Controller
    {
        private const long MaxBytes = 50 * 1024 * 1024;
        private static readonly string[] AllowedTypes = { "image/jpeg", "image/png", "image/tiff", "image/webp" };

        private readonly IBlobService _blobs;
        private readonly TelemetryService _telemetry;
        private readonly AppDbContext _db;
        private readonly ILogger<MediaController> _logger;

        public MediaController(IBlobService blobs, TelemetryService telemetry, AppDbContext db, ILogger<MediaController> logger)
        {
            _blobs = blobs;
            _telemetry = telemetry;
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new MediaViewModel
            {
                Farms = await _db.Devices.Select(d => d.FarmName).Distinct().OrderBy(f => f).ToListAsync()
            };
            try
            {
                vm.Images = await _blobs.ListAsync();
                vm.Audit = await _telemetry.GetAuditAsync(10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not load media");
                TempData["Error"] = "Could not load media from Azure Storage.";
            }
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxBytes)]
        public async Task<IActionResult> Upload(IFormFile? file, string farm)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please choose an image to upload.";
                return RedirectToAction(nameof(Index));
            }
            if (file.Length > MaxBytes || !AllowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                TempData["Error"] = "Only JPEG, PNG, TIFF or WebP images up to 50 MB are allowed.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var name = await _blobs.UploadAsync(file, farm ?? "", User.Identity?.Name ?? "unknown");
                TempData["Message"] = $"Uploaded {name}. The blob-triggered function will log it shortly.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");
                TempData["Error"] = "Upload failed. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
