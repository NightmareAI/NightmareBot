using System.Reflection;
using Dapr.Client;
using Discord.Commands;
using Discord.WebSocket;
using NightmareBot.Models;

namespace NightmareBot.Handlers;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private IServiceProvider? _serviceProvider = null;
    private ILogger<CommandHandler> _logger;

    public CommandHandler(DiscordSocketClient client, CommandService service)
    {
        _client = client;
        _commands = service;
    }

    public async Task InstallCommandsAsync(IServiceProvider serviceProvider)
    {
        _client.MessageReceived += HandleCommandAsync;
        _client.ButtonExecuted += HandleButtonAsync;
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<CommandHandler>>();
        await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: serviceProvider);
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
                            channel = component.ChannelId.ToString(), message = component.Message.Id.ToString(),
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

                    using var daprClient = _serviceProvider.GetRequiredService<DaprClient>();
                    await daprClient.PublishEventAsync("servicebus-pubsub", $"request.{request.request_type}", request);
                    await component.RespondAsync("Enhancing...");
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