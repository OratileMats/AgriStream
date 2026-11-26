using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 1 - Blob trigger.
/// Fires when a drone image lands in the "drone-images" container. Logs the upload details
/// and writes a processing-status audit entry into the "ProcessingAudit" Azure Table.
/// </summary>
public class BlobUploadAuditFunction
{
    private readonly ILogger<BlobUploadAuditFunction> _logger;

    public BlobUploadAuditFunction(ILogger<BlobUploadAuditFunction> logger) => _logger = logger;

    [Function("BlobUploadAudit")]
    public async Task Run(
        [BlobTrigger("drone-images/{name}", Connection = "AzureWebJobsStorage")] BlobClient blob,
        string name)
    {
        var props = (await blob.GetPropertiesAsync()).Value;
        props.Metadata.TryGetValue("farm", out var farm);
        props.Metadata.TryGetValue("uploadedBy", out var uploadedBy);

        _logger.LogInformation(
            "Drone image uploaded: {Name} ({Bytes} bytes, {Type}) farm={Farm} by={User}",
            name, props.ContentLength, props.ContentType, farm, uploadedBy);

        var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                   ?? throw new InvalidOperationException("AzureWebJobsStorage is not configured.");
        var table = new TableClient(conn, "ProcessingAudit");
        await table.CreateIfNotExistsAsync();

        var now = DateTimeOffset.UtcNow;
        var entity = new TableEntity(
            partitionKey: "drone-images",
            rowKey: $"{(DateTime.MaxValue.Ticks - now.UtcTicks):D19}_{Guid.NewGuid():N}")
        {
            ["BlobName"] = name,
            ["SizeBytes"] = props.ContentLength,
            ["ContentType"] = props.ContentType,
            ["Farm"] = farm ?? "",
            ["UploadedBy"] = uploadedBy ?? "",
            ["UploadedAt"] = props.CreatedOn,
            ["Status"] = "Received",
            ["ProcessedAt"] = now
        };
        await table.AddEntityAsync(entity);

        _logger.LogInformation("Audit entry written for {Name}", name);
    }
}
