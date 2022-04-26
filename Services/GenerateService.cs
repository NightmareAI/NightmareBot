using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using NightmareBot.Models;

namespace NightmareBot.Services;

public class GenerateService : BackgroundService
{
    public readonly IServiceScopeFactory ScopeFactory;
    public ConcurrentQueue<PredictionRequest<Laionidev3Input>> Laionidev3RequestQueue;
    public ConcurrentQueue<PredictionRequest<ClipDrawInput>> ClipdrawRequestQueue;

    public GenerateService(IServiceScopeFactory scopeFactory)
    {
        ScopeFactory = scopeFactory;
        Laionidev3RequestQueue = new ConcurrentQueue<PredictionRequest<Laionidev3Input>>();
        ClipdrawRequestQueue = new ConcurrentQueue<PredictionRequest<ClipDrawInput>>();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = ScopeFactory.CreateScope();
            if (Laionidev3RequestQueue.TryDequeue(out var request))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GenerateLaionideV3(request, discordClient);
            }

            if (ClipdrawRequestQueue.TryDequeue(out var clipdrawRequest))
            {
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                await this.GenerateClipdraw(clipdrawRequest, discordClient);
            }
        }
    }
    
    private async Task GenerateLaionideV3(PredictionRequest<Laionidev3Input> request, DiscordSocketClient client)
    {
        var guild = client.GetGuild(request.GuildId);
        var channel = guild.GetTextChannel(request.ChannelId);
        var messageReference = new MessageReference(request.MessageId, request.ChannelId, request.GuildId);

        await client.SetGameAsync(request.input.prompt, null, ActivityType.Playing);
        
        var httpClient = new HttpClient();
        try
        {
            var result = await httpClient.PostAsJsonAsync("http://localhost:5000/predictions", request);
            if (!result.IsSuccessStatusCode)
            {
                await channel.SendMessageAsync($"Sorry, it's fucked. [{result.StatusCode}]");
                return;
            }
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }

        var attachmentPath =
            $"{Directory.GetCurrentDirectory()}\\result\\{request.Id}\\upsample_predictions.png";

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        await channel.SendFileAsync(attachmentPath, $"> {request.input.prompt} (Seed: {request.input.seed})", messageReference: messageReference);
    }

    private async Task GenerateClipdraw(PredictionRequest<ClipDrawInput> request, DiscordSocketClient client)
    {
        var guild = client.GetGuild(request.GuildId);
        var channel = guild.GetTextChannel(request.ChannelId);
        var messageReference = new MessageReference(request.MessageId, request.ChannelId, request.GuildId);

        await client.SetGameAsync(request.input.prompt, null, ActivityType.Playing);
        
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        try
        {
            var result = await httpClient.PostAsJsonAsync("http://localhost:5000/predictions", request);

            if (!result.IsSuccessStatusCode)
            {
                await channel.SendMessageAsync($"Sorry, it's fucked. [{result.StatusCode}]");
                return;
            }
        }
        catch (Exception ex)
        {
            await channel.SendMessageAsync($"Help, I'm exploding. {ex.Message}");
            return;
        }

        var attachmentPath =
            $"{Directory.GetCurrentDirectory()}\\result\\{request.Id}\\out.png";

        await client.SetGameAsync("the art critics", null, ActivityType.Listening);
        await channel.SendFileAsync(attachmentPath, $"> {request.input.prompt}", messageReference: messageReference);

    }

}