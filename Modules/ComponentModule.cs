using Dapr.Client;
using Discord.Interactions;
using Discord.WebSocket;
using NightmareBot.Models;

namespace NightmareBot.Modules;

public class ComponentModule : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    private readonly ILogger<ComponentModule> _logger;

    public ComponentModule(ILogger<ComponentModule> logger)
    {
        _logger = logger;
    }

    [ComponentInteraction("enhance|*|*")]
    private async Task EnhanceAsync(string id, string image)
    {
        try
        {
            var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";
            var request = new PredictionRequest<SwinIRInput>(Context, new SwinIRInput{ images = new[] { imageUrl }}, Guid.NewGuid());

            using var daprClient = new DaprClientBuilder().Build();
            await daprClient.PublishEventAsync("servicebus-pubsub", $"request.{request.request_type}", request);
            await daprClient.SaveStateAsync("statestore", request.id.ToString(), request);
            await DeferAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling enhance request");
        }
    }

}