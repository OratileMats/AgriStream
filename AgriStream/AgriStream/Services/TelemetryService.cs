using AgriStream.Models;
using Azure.Data.Tables;

namespace AgriStream.Services
{
    /// <summary>
    /// Azure Table Storage access from the MVC app (task c).
    /// Telemetry: PartitionKey = UTC date (yyyyMMdd), RowKey = reverse ticks + sensor id (newest first).
    /// </summary>
    public class TelemetryService
    {
        public const string TelemetryTable = "Telemetry";
        public const string AuditTable = "ProcessingAudit";

        private readonly TableClient _telemetry;
        private readonly TableClient _audit;
        private volatile bool _ready;

        public TelemetryService(IConfiguration config)
        {
            var cs = config.GetConnectionString("AzureBlobStorage")
                     ?? throw new InvalidOperationException("ConnectionStrings:AzureBlobStorage is not configured.");
            var svc = new TableServiceClient(cs);
            _telemetry = svc.GetTableClient(TelemetryTable);
            _audit = svc.GetTableClient(AuditTable);
        }

        private async Task EnsureAsync()
        {
            if (_ready) return;
            await _telemetry.CreateIfNotExistsAsync();
            await _audit.CreateIfNotExistsAsync();
            _ready = true;
        }

        public async Task AddReadingAsync(TelemetryDto dto)
        {
            await EnsureAsync();
            var now = DateTimeOffset.UtcNow;
            dto.RecordedAt = now;

            var entity = new TableEntity(
                partitionKey: now.ToString("yyyyMMdd"),
                rowKey: $"{(DateTime.MaxValue.Ticks - now.UtcTicks):D19}_{dto.SensorId}")
            {
                ["SensorId"] = dto.SensorId,
                ["FarmName"] = dto.FarmName,
                ["SoilMoisture"] = dto.SoilMoisture,
                ["TemperatureC"] = dto.TemperatureC,
                ["Humidity"] = dto.Humidity,
                ["Status"] = dto.Status,
                ["RecordedAt"] = now
            };
            await _telemetry.AddEntityAsync(entity);
        }

        /// <summary>Reads the processing-status audit entries written by the Blob-triggered function.</summary>
        public async Task<List<AuditEntry>> GetAuditAsync(int top = 10)
        {
            await EnsureAsync();
            var list = new List<AuditEntry>();
            await foreach (var e in _audit.QueryAsync<TableEntity>(maxPerPage: top))
            {
                list.Add(new AuditEntry
                {
                    BlobName = e.GetString("BlobName") ?? e.RowKey,
                    SizeBytes = e.GetInt64("SizeBytes") ?? 0,
                    Farm = e.GetString("Farm") ?? "",
                    UploadedBy = e.GetString("UploadedBy") ?? "",
                    Status = e.GetString("Status") ?? "",
                    ProcessedAt = e.GetDateTimeOffset("ProcessedAt")
                });
                if (list.Count >= top) break;
            }
            return list;
        }
    }
}
