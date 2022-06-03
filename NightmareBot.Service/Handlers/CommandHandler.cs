using System.Reflection;
using Dapr.Client;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using NightmareBot.Common;
using NightmareBot.Models;

namespace NightmareBot.Handlers;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly DaprClient _daprClient;
    private readonly CommandService _commands;
    private readonly InteractionService _interactions;
    private IServiceProvider? _serviceProvider = null;
    private ILogger<CommandHandler> _logger;
    private readonly BotLogger _botLogger;

    public CommandHandler(DiscordSocketClient client, CommandService service, InteractionService interactionService, DaprClient daprClient, ILogger<CommandHandler> logger, BotLogger botLogger)
    {
        _client = client;
        _commands = service;
        _interactions = interactionService;
        _daprClient = daprClient;
        _logger = logger;
        _botLogger = botLogger;
    }

    public async Task InstallCommandsAsync(IServiceProvider serviceProvider)
    {
        _client.MessageReceived += HandleCommandAsync;
        //_client.ButtonExecuted += HandleButtonAsync;
        _client.InteractionCreated += HandleInteraction;
        _client.Ready += ReadyAsync;
        _serviceProvider = serviceProvider;
        await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: serviceProvider);
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);
    }

    private async Task ReadyAsync()
    {
        await _interactions.AddCommandsGloballyAsync(true);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, _serviceProvider);

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        break;
                    default:
                        break;
                }
                _logger.LogError($"Failed to queue: {result.Error}: {result.ErrorReason}");
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => { await msg.Result.ModifyAsync(p => p.Content = $"Failed to queue: {result.ErrorReason}"); });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue request");
            if (interaction.Type is Discord.InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }

    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        var idParts = component.Data.CustomId.Split("|");
        switch (idParts[0])
        {
            case "enhance":
            {
                try
                {
                    var imageUrl = $"https://dumb.dev/nightmarebot-output/{idParts[1]}/{idParts[2]}";
                    var request = new PredictionRequest<SwinIRInput>()
                    {

                        context = new DiscordContext
                        {
                            channel = component.ChannelId.ToString(), 
                            message = component.Message.Id.ToString(),
                            user = component.User.Id.ToString(),
                            guild = (component.Channel as SocketGuildChannel).Guild.Id.ToString()
                        },
                        id = Guid.NewGuid(),
                        request_time = DateTime.UtcNow,
                        input = new SwinIRInput
                        {
                            images = new[] { imageUrl }
                        }
                    };

                    using var daprClient = new DaprClientBuilder().Build();
                    await daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
                    await daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
                    await component.DeferAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling enhance request");
                }
                break;
            }
        }
    }

    private async Task HandleCommandAsync(SocketMessage messageParam)
    {
        if (messageParam is not SocketUserMessage message) return;

        int argPos = 0;

        if (!(message.HasCharPrefix('!', ref argPos) ||
              message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return;

        var context = new SocketCommandContext(_client, message);

        await _commands.ExecuteAsync(
            context: context,
            argPos: argPos,
            services: _serviceProvider);
    }
}