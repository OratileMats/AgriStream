using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 2 - HTTP GET.
/// Called by the MVC dashboard. Returns the newest telemetry rows from Table Storage.
/// Optional query string: ?top=40 (1-200)  &amp;sensorId=SEN001
/// Rows are partitioned by UTC date and keyed by reverse ticks, so the newest rows are read first
/// with a single-partition query (fast, cheap and scalable).
/// </summary>
public class GetLatestTelemetryFunction
{
    private readonly ILogger<GetLatestTelemetryFunction> _logger;

    public GetLatestTelemetryFunction(ILogger<GetLatestTelemetryFunction> logger) => _logger = logger;

    [Function("GetLatestTelemetry")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry")] HttpRequest req)
    {
        var top = int.TryParse(req.Query["top"].ToString(), out var t) ? Math.Clamp(t, 1, 200) : 40;
        var sensorId = req.Query["sensorId"].ToString();

        var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrEmpty(conn)) return new StatusCodeResult(500);

        var table = new TableClient(conn, "Telemetry");
        await table.CreateIfNotExistsAsync();

        var results = new List<object>();
        // today first, then yesterday if we still need more rows (handles the UTC midnight rollover)
        foreach (var day in new[] { DateTime.UtcNow, DateTime.UtcNow.AddDays(-1) })
        {
            if (results.Count >= top) break;

            var filter = $"PartitionKey eq '{day:yyyyMMdd}'";
            if (!string.IsNullOrWhiteSpace(sensorId))
                filter += $" and SensorId eq '{sensorId.Replace("'", "''")}'";

            await foreach (var e in table.QueryAsync<TableEntity>(filter, maxPerPage: top))
            {
                results.Add(new
                {
                    sensorId = e.GetString("SensorId"),
                    farmName = e.GetString("FarmName"),
                    soilMoisture = e.GetDouble("SoilMoisture") ?? 0,
                    temperatureC = e.GetDouble("TemperatureC") ?? 0,
                    humidity = e.GetDouble("Humidity") ?? 0,
                    status = e.GetString("Status") ?? "Active",
                    recordedAt = e.GetDateTimeOffset("RecordedAt") ?? e.Timestamp
                });
                if (results.Count >= top) break;
            }
        }

        _logger.LogInformation("Returned {Count} telemetry rows", results.Count);
        return new OkObjectResult(results);
    }
}
