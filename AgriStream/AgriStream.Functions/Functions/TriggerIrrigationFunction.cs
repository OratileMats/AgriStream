using System.Text.Json;
using AgriStream.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 3 - HTTP POST.
/// Called by the MVC app when a sensor flags a problem or a user clicks "Trigger Irrigation".
/// Validates the request and places an Irrigation Command message on the Storage Queue.
/// </summary>
public class TriggerIrrigationFunction
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<TriggerIrrigationFunction> _logger;

    public TriggerIrrigationFunction(ILogger<TriggerIrrigationFunction> logger) => _logger = logger;

    [Function("TriggerIrrigation")]
    public async Task<TriggerIrrigationOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation")] HttpRequest req)
    {
        IrrigationRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<IrrigationRequest>(req.Body, Json);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body == null || string.IsNullOrWhiteSpace(body.DeviceId) ||
            (body.Action != "Start" && body.Action != "Stop"))
        {
            return new TriggerIrrigationOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "deviceId and action (Start|Stop) are required." })
            };
        }

        var command = new IrrigationCommand
        {
            DeviceId = body.DeviceId.Trim(),
            Action = body.Action,
            RequestedBy = body.RequestedBy,
            Reason = body.Reason
        };

        _logger.LogInformation("Queueing irrigation command {Id}: {Action} {Device} (by {By})",
            command.CommandId, command.Action, command.DeviceId, command.RequestedBy);

        return new TriggerIrrigationOutput
        {
            QueueMessage = JsonSerializer.Serialize(command),
            HttpResponse = new ObjectResult(new { commandId = command.CommandId, status = "Queued" })
            {
                StatusCode = StatusCodes.Status202Accepted
            }
        };
    }
}

public class TriggerIrrigationOutput
{
    [QueueOutput("irrigation-commands", Connection = "AzureWebJobsStorage")]
    public string? QueueMessage { get; set; }

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = null!;
}
