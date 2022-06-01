using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Dapr;
using Dapr.Client;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NightmareBot.Models;

namespace NightmareBot.Controllers;

[ApiController]
[Route("[controller]")]
public class ResultController : ControllerBase
{
    private readonly ILogger<ResultController> _logger;

    public ResultController(ILogger<ResultController> logger)
    {
        _logger = logger;
    }

    [Topic("jetstream-pubsub", "response.swinir")]
    [Route("swinir")]
    [HttpPost]
    public async Task<ActionResult> SwinIRResponse(ResponseModel response, [FromServices] DaprClient daprClient,
        [FromServices] DiscordSocketClient discordClient, [FromServices] InteractionService interactionService)
    {
        try
        {
            var prompt = await daprClient.GetStateAsync<string>("cosmosdb", $"prompts-{response.id}");
            if (!response.images.Any())
                return Ok();
            
            ulong.TryParse(response.context.channel, out var channel_id);
            ulong.TryParse(response.context.guild, out var guild_id);
            ulong.TryParse(response.context.message, out var message_id);
            ulong.TryParse(response.context.user, out var user_id);
            ulong.TryParse(response.context.interaction, out var interaction_id);
            var guild = discordClient.GetGuild(guild_id);
            var channel = guild.GetTextChannel(channel_id);
            var embeds = new List<Embed>();
            var messageText = new StringBuilder();
            if (!string.IsNullOrEmpty(prompt))
                messageText.AppendLine($"> {prompt}");
            var builder = new ComponentBuilder();
            int ix = 1;

            if (!string.IsNullOrEmpty(response.error))
            {
                messageText.AppendLine($"Failed due to error: {response.error}");
                messageText.AppendLine(MentionUtils.MentionUser(user_id));
                await channel.SendMessageAsync(messageText.ToString());
                return Ok();
            }

            foreach (var image in response.images)
            {
                var eb = new EmbedBuilder();
                eb.WithImageUrl($"https://dumb.dev/nightmarebot-output/{response.id}/{image}");
                embeds.Add(eb.Build());
                string label = "Tweet";
                if (response.images.Length > 1)
                    label += $" {ix++}";
                builder.WithButton(label, $"tweet:{response.id},{image}", ButtonStyle.Success);
            }

            messageText.AppendLine(MentionUtils.MentionUser(user_id));
            await channel.SendMessageAsync(messageText.ToString(), embeds: embeds.ToArray(), components: builder.Build());

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Failed to respond to swinir results");
            return BadRequest();
        }
    }
    
    [Topic("jetstream-pubsub", "response.latent-diffusion")]
    [Route("latent-diffusion")]
    [HttpPost]
    public async Task<ActionResult> LatentDiffusionResponse(ResponseModel response, [FromServices] DaprClient daprClient, [FromServices] DiscordSocketClient discordClient, [FromServices] InteractionService interactionService) 
    {
        try
        {
            // Retrieve request from state store by ID
            var request =
            await daprClient.GetStateAsync<PredictionRequest<LatentDiffusionInput>>("cosmosdb",
                    response.id.ToString());

            _logger.LogInformation(
                $"Context: Guild {request.context.guild} Channel {request.context.channel} Message {request.context.message} User {request.context.user}");
            ulong.TryParse(request.context.guild, out var guild_id);
            ulong.TryParse(request.context.channel, out var channel_id);
            ulong.TryParse(request.context.message, out var message_id);
            ulong.TryParse(request.context.user, out var user_id);

            var message =
                $"> {request.input.prompt}\n(latent-diffusion, {(DateTime.UtcNow - request.request_time).TotalSeconds} seconds end to end)\n";

            var guild = discordClient.GetGuild(guild_id);
            if (guild == null)
            {
                _logger.LogWarning("Unable to get guild from discord");
                return BadRequest();
            }
            var channel = guild.GetTextChannel(channel_id);


            if (!string.IsNullOrEmpty(response.error))
            {
                message += $"Failed due to error: {response.error}\n";
                message +=  MentionUtils.MentionUser(user_id);
                await channel.SendMessageAsync(message);
                return Ok();
            }

            request.sample_filenames = response.images;
            request.complete_time = DateTime.UtcNow;
            await daprClient.SaveStateAsync("cosmosdb", response.id.ToString(), request);
            var builder = new ComponentBuilder();
            List <ActionRowBuilder> actions = new List <ActionRowBuilder>();
            ActionRowBuilder enhanceButtons = new ActionRowBuilder();
            ActionRowBuilder generateButtons = new ActionRowBuilder();
            for (int ix = 0; ix < response.images.Length; ix++)
            {
                enhanceButtons.WithButton($"Enhance {ix + 1}", $"enhance:{response.id},samples/{response.images[ix]}", ButtonStyle.Primary);
                generateButtons.WithButton($"Dream {ix + 1}", $"pixray_init:{response.id},samples/{response.images[ix]}", ButtonStyle.Secondary);
            }
            actions.Add(enhanceButtons);
            actions.Add(generateButtons);
            builder.WithRows(actions);

            var embed = new EmbedBuilder();
            embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{response.id}/results.png");

            message += MentionUtils.MentionUser(user_id);
            await channel.SendMessageAsync(message, embed: embed.Build(), components: builder.Build());


            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Failed to respond");
            return BadRequest();
        }
    }

    [Route("pixray/{id}")]
    [HttpPost]
    public async Task<ActionResult> PixraySimpleResponse(Guid id, [FromServices] DaprClient daprClient, [FromServices] DiscordSocketClient discordClient, [FromServices] InteractionService interactionService)
    {
        return await PixrayResponse(new ResponseModel { id = id }, daprClient, discordClient, interactionService);
    }

    [Topic("jetstream-pubsub", "response.pixray")]
    [Route("pixray")]
    [HttpPost]
    public async Task<ActionResult> PixrayResponse(ResponseModel response, [FromServices] DaprClient daprClient, [FromServices] DiscordSocketClient discordClient, [FromServices] InteractionService interactionService)
    {
        try
        {
            var request = await daprClient.GetStateAsync<PredictionRequest<PixrayInput>>("cosmosdb", response.id.ToString());
            
            ulong.TryParse(request.context.guild, out var guild_id);
            ulong.TryParse(request.context.channel, out var channel_id);
            ulong.TryParse(request.context.message, out var message_id);
            ulong.TryParse(request.context.user, out var user_id);

            var message =
                $"```{request.input.settings}```\n(pixray, {(DateTime.UtcNow - request.request_time).TotalSeconds} seconds end to end)\n" +
                $"https://dumb.dev/nightmarebot-output/{response.id}/steps/output.mp4\n";

            request.sample_filenames = response.images;
            request.complete_time = DateTime.UtcNow;
            await daprClient.SaveStateAsync("cosmosdb", response.id.ToString(), request);


            var embed = new EmbedBuilder();
            embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{response.id}/output.png");
 
            var builder = new ComponentBuilder();
            builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Primary).WithCustomId($"enhance:{response.id},output.png").WithLabel("Enhance"));
            builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"pixray_init:{response.id},output.png").WithLabel("Dream"));

            var guild = discordClient.GetGuild(guild_id);
            if (guild == null)
            {
                _logger.LogWarning("Unable to get guild from discord");
                return BadRequest();
            }
            var channel = guild.GetTextChannel(channel_id);

            if (!string.IsNullOrEmpty(response.error))
            {
                message += $"Failed due to error: {response.error}\n";
                message += MentionUtils.MentionUser(user_id);
                await channel.SendMessageAsync(message);
                return Ok();
            }


            message += MentionUtils.MentionUser(user_id);
            await channel.SendMessageAsync(message, embed: embed.Build(), components: builder.Build());


            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to respond");
            return BadRequest();
        }
    }


    [HttpGet("{path}/{filename}.png")]
    public async Task<ActionResult> Get(string path, string filename)
    {
        var file = await System.IO.File.ReadAllBytesAsync($"/home/palp/NightmareBot/result/{path}/{filename}.png");
        return File(file, "image/png", filename + ".png");
    }
    
    [HttpPut("{path}/{filename}.png")]
    public async Task<IActionResult> Put(string path, string filename)
    {
        var file = this.Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            return BadRequest();
        }

        string outPath = $"result/{path}";
        Directory.CreateDirectory(outPath);
        await using var outFile = new FileStream($"{outPath}/{filename}.png", FileMode.Create);
        await file.CopyToAsync(outFile);
        return Ok();
    }
}