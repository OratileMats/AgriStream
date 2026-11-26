namespace AgriStream.Functions.Models;

/// <summary>Body of POST /api/irrigation sent by the MVC app.</summary>
public class IrrigationRequest
{
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>"Start" or "Stop"</summary>
    public string Action { get; set; } = "Start";
    public string RequestedBy { get; set; } = "unknown";
    public string? Reason { get; set; }
}

/// <summary>Message placed on the "irrigation-commands" queue.</summary>
public class IrrigationCommand
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public string Action { get; set; } = "Start";
    public string RequestedBy { get; set; } = "unknown";
    public string? Reason { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
