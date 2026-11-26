using System.ComponentModel.DataAnnotations;

namespace AgriStream.Models
{
    public class RegisterViewModel
    {
        [Required, StringLength(50, MinimumLength = 3)]
        public string? Username { get; set; }

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }
    }

    /// <summary>Telemetry reading as stored in Table Storage / returned by the GET function.</summary>
    public class TelemetryDto
    {
        public string SensorId { get; set; } = string.Empty;
        public string FarmName { get; set; } = string.Empty;
        public double SoilMoisture { get; set; }
        public double TemperatureC { get; set; }
        public double Humidity { get; set; }
        public string Status { get; set; } = "Active";
        public DateTimeOffset RecordedAt { get; set; }
    }

    /// <summary>Payload sent by a sensor to POST /api/telemetry.</summary>
    public class TelemetryIngestRequest
    {
        [Required, MaxLength(20)] public string SensorId { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string FarmName { get; set; } = string.Empty;
        [Range(0, 100)] public double SoilMoisture { get; set; }
        [Range(-30, 70)] public double TemperatureC { get; set; }
        [Range(0, 100)] public double Humidity { get; set; }
    }

    public class IrrigationRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        /// <summary>"Start" or "Stop".</summary>
        public string Action { get; set; } = "Start";
        public string RequestedBy { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class DroneImage
    {
        public string Name { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTimeOffset? UploadedOn { get; set; }
        public string Farm { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class AuditEntry
    {
        public string BlobName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Farm { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? ProcessedAt { get; set; }
    }

    public class DashboardViewModel
    {
        public int UserCount { get; set; }
        public int DeviceCount { get; set; }
        public int IrrigatingCount { get; set; }
        public List<AuditEntry> RecentAudit { get; set; } = new();
    }

    public class MediaViewModel
    {
        public List<DroneImage> Images { get; set; } = new();
        public List<AuditEntry> Audit { get; set; } = new();
        public List<string> Farms { get; set; } = new();
    }
}
