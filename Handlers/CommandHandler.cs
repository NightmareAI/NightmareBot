using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;

namespace NightmareBot.Handlers;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private IServiceProvider? _serviceProvider = null;
    
    public CommandHandler(DiscordSocketClient client, CommandService service)
    {
        _client = client;
        _commands = service;
    }

    public async Task InstallCommandsAsync(IServiceProvider serviceProvider)
    {
        _client.MessageReceived += HandleCommandAsync;
        _serviceProvider = serviceProvider;
        await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: serviceProvider);
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