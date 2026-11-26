using System.Text.Json;
using AgriStream.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 4 - Queue trigger.
/// Listens to "irrigation-commands", processes each command and updates the device's
/// operational status in the master registry table (dbo.Devices) in Azure SQL Database.
/// After maxDequeueCount failures the message moves to "irrigation-commands-poison".
/// </summary>
public class ProcessIrrigationCommandFunction
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<ProcessIrrigationCommandFunction> _logger;

    public ProcessIrrigationCommandFunction(ILogger<ProcessIrrigationCommandFunction> logger) => _logger = logger;

    [Function("ProcessIrrigationCommand")]
    public async Task Run(
        [QueueTrigger("irrigation-commands", Connection = "AzureWebJobsStorage")] string message)
    {
        var cmd = JsonSerializer.Deserialize<IrrigationCommand>(message, Json);
        if (cmd == null || string.IsNullOrWhiteSpace(cmd.DeviceId))
        {
            _logger.LogError("Discarding malformed irrigation command: {Message}", message);
            return;
        }

        var status = cmd.Action == "Stop" ? "Idle" : "Irrigating";
        var summary = $"{cmd.Action} by {cmd.RequestedBy} at {cmd.RequestedAt:u}" +
                      (string.IsNullOrWhiteSpace(cmd.Reason) ? "" : $" - {cmd.Reason}");
        if (summary.Length > 200) summary = summary[..200];

        var connStr = Environment.GetEnvironmentVariable("SqlConnectionString")
                      ?? throw new InvalidOperationException("SqlConnectionString app setting is missing.");

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        await using var sql = new SqlCommand(
            @"UPDATE dbo.Devices
                 SET Status = @status, LastCommand = @last, LastUpdated = SYSUTCDATETIME()
               WHERE DeviceId = @id", conn);
        sql.Parameters.AddWithValue("@status", status);
        sql.Parameters.AddWithValue("@last", summary);
        sql.Parameters.AddWithValue("@id", cmd.DeviceId);

        var rows = await sql.ExecuteNonQueryAsync();
        if (rows == 0)
            _logger.LogWarning("Command {Id}: device {Device} not found in registry", cmd.CommandId, cmd.DeviceId);
        else
            _logger.LogInformation("Command {Id}: device {Device} is now {Status}", cmd.CommandId, cmd.DeviceId, status);
    }
}
