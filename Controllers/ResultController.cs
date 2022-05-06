using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Dapr;
using Dapr.Client;
using Discord;
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

    [Topic("discord-workqueue", "response.latent-diffusion")]
    [Route("latent-diffusion")]
    [HttpPost]
    public async Task<ActionResult> LatentDiffusionResponse(ResponseModel response, [FromServices] DaprClient daprClient, [FromServices] DiscordSocketClient discordClient) 
    {
        try
        {
            // Retrieve request from state store by ID
            var request =
            await daprClient.GetStateAsync<PredictionRequest<LatentDiffusionInput>>("statestore",
                    response.id.ToString());

            _logger.LogInformation(
                $"Context: Guild {request.context.guild} Channel {request.context.channel} Message {request.context.message} User {request.context.user}");
            var guild_id = ulong.Parse(request.context.guild);
            var guild = discordClient.GetGuild(guild_id);
            if (guild == null)
            {
                _logger.LogWarning("Unable to get guild from discord");
                return BadRequest();
            }

            var channel_id = ulong.Parse(request.context.channel);
            var channel = guild.GetTextChannel(channel_id);
            var messageReference = new MessageReference(ulong.Parse(request.context.message), channel_id, guild_id);

            var message =
                $"> {request.input.prompt}\n(latent-diffusion, {(DateTime.UtcNow - request.request_time).TotalSeconds} seconds end to end)\n" +
                $"https://dumb.dev/nightmarebot-output/{response.id}/results.png\n";

            request.sample_filenames = response.images;
            request.complete_time = DateTime.UtcNow;
            await daprClient.SaveStateAsync("statestore", response.id.ToString(), request);
            var builder = new ComponentBuilder();
            for (int ix = 0; ix < response.images.Length; ix++)
                builder.WithButton(new ButtonBuilder().
                    WithStyle(ButtonStyle.Primary).
                    WithCustomId(response.images[ix]).
                    WithLabel($"Enhance {ix+1}"));

            await channel.SendMessageAsync(message, messageReference: messageReference, components: builder.Build());
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Failed to respond");
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