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
    private readonly DiscordSocketClient _discord;

    public ResultController(ILogger<ResultController> logger, DiscordSocketClient discord)
    {
        _logger = logger;
        _discord = discord;
    }

    [Topic("pubsub", "response.latent-diffusion")]
    [HttpPost("latent-diffusion")]
    public async Task<ActionResult> LatentDiffusionResponse([FromBody] PredictionRequest<LatentDiffusionInput> request) 
    {
        var guild = _discord.GetGuild(request.guild_id);
        var channel = guild.GetTextChannel(request.channel_id);
        var messageReference = new MessageReference(request.message_id, request.channel_id, request.guild_id);

        var message = $"> {request.input.prompt}\n(latent-diffusion, {(DateTime.UtcNow - request.request_time).TotalSeconds} seconds)\n";
        foreach (var sample in request.sample_filenames)
        {
            message += $"https://dumb.dev/nightmarebot-output/{request.id}/samples/{sample}\n";
        }

        await channel.SendMessageAsync(message.ToString(), messageReference: messageReference);
        return Ok();
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