using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NightmareBot.Common;
using Discord;
using System.Collections.Generic;
using Discord.Rest;
using OpenAI;
using System.Linq;
using Minio;
using System.Text.Json;
using System.Net.Http;
using NightmareBot.Common.RunPod;

namespace NightmareBot.Functions
{
    public static class DreamerFunctions
    {
        [FunctionName("ProcessResult")]
        public static async Task<IActionResult> ProcessResult(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {            
            string token = req.Query["token"];
            string requestId = req.Query["id"];
            log.LogInformation($"Processing response for {requestId}");
            var minioClient = new Minio.MinioClient().WithCredentials(Environment.GetEnvironmentVariable("NIGHTMAREBOT_MINIO_KEY"), Environment.GetEnvironmentVariable("NIGHTMAREBOT_MINIO_SECRET")).WithEndpoint("dumb.dev").Build();
            DiscordContext context = null;
            await minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{requestId}/context.json").WithCallbackStream(s => { context = JsonSerializer.Deserialize<DiscordContext>(s); }));
            string prompt = "";
            await minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{requestId}/prompt.txt").WithCallbackStream(s => { prompt = new StreamReader(s).ReadToEnd(); }));
            RequestState state = null;
            try
            {
                await minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{requestId}/state.json").WithCallbackStream(s => { state = JsonSerializer.Deserialize<RequestState>(s); }));
                state.success = true;
                state.completed_at = DateTime.UtcNow;
                state.last_updated = DateTime.UtcNow;
                state.is_active = false;
            }
            catch { } // this is fine

            if (string.IsNullOrEmpty(prompt))
            {
                log.LogError("Prompt is null");
                return new BadRequestResult();
            }

            if (context != null && !string.IsNullOrWhiteSpace(prompt))
                await MajestyRespond(requestId, context, prompt, $"{requestId}.png", state, log);

            return new OkResult();
        }

        [FunctionName("IdleWorker")]
        public static async Task<IActionResult> IdleWorker(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            string token = req.Query["token"];
            string podId = req.Query["id"];
            string env = req.Query["env"];
            string apiKey = Environment.GetEnvironmentVariable("NIGHTMAREBOT_RUNPOD_KEY");
            if (env == "runpod")
            {
                // Hack
                var content = await new RunPodApiClient(apiKey).StopPod(podId);
                return new OkObjectResult(content);
            }

            return new BadRequestResult();
        }

        private static async Task MajestyRespond(string id, DiscordContext context, string prompt, string filename, RequestState request, ILogger log)
        {
            if (context == null)
                return;

            log.LogInformation("Parsing context..");
            ulong.TryParse(context.guild, out var guild_id);
            ulong.TryParse(context.channel, out var channel_id);
            ulong.TryParse(context.message, out var message_id);
            ulong.TryParse(context.user, out var user_id);
            var token = Environment.GetEnvironmentVariable("NIGHTMAREBOT_DISCORD_TOKEN");

            log.LogInformation("Building compoenent...");
            var builder = new ComponentBuilder();
            builder.WithSelectMenu
                ($"enhance-select-direct:{id},{filename}", new List<SelectMenuOptionBuilder>
                {
            new SelectMenuOptionBuilder().WithValue("swinir").WithLabel("SwinIR").WithDescription("Uses SwinIR to upscale 4x"),
            new SelectMenuOptionBuilder().WithValue("esrgan").WithLabel("Real-ESRGAN").WithDescription("Uses Real-ESRGAN to upscale 6x"),
            new SelectMenuOptionBuilder().WithValue("esrgan-face").WithLabel("Real-ESRGAN-Face").WithDescription("Real-ESRGAN+GFPGAN face restoration")
                }, minValues: 1, maxValues: 1, placeholder: "Enhance");
            builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"dream:{id},{filename}").WithLabel("Dream"));
            builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"pixray_init:{id},{filename}").WithLabel("Pixray"));

            var discord = new DiscordRestClient();
            try
            {
                log.LogInformation("Authenticating to Discord...");                
                await discord.LoginAsync(TokenType.Bot, token);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to login to Discord");
            }

            log.LogInformation("Getting guild...");
            var guild = await discord.GetGuildAsync(guild_id);
            if (guild == null)
            {
                Console.WriteLine("Unable to get guild from discord");
                return;
            }

            log.LogInformation("Getting channel...");
            var channel = await guild.GetTextChannelAsync(channel_id);
            log.LogInformation("Getting user...");
            var user = await channel.GetUserAsync(user_id);


            log.LogInformation("Building fields");
            var fields = new List<EmbedFieldBuilder>();

            fields.Add(new EmbedFieldBuilder().WithName("dreamer").WithValue("majesty-diffusion").WithIsInline(true));
            fields.Add(new EmbedFieldBuilder().WithName("prompt").WithValue(prompt));            
            if (request?.created_at != null && request?.completed_at != null)
                fields.Add(new EmbedFieldBuilder().WithName("time elapsed").WithValue($"{(request.completed_at - request.created_at)?.TotalSeconds} seconds"));
            if (!string.IsNullOrWhiteSpace(request?.preset_name))
                fields.Add(new EmbedFieldBuilder().WithName("preset").WithValue(request.preset_name).WithIsInline(true));

            using var typingState = channel.EnterTypingState();
            var embed = new EmbedBuilder();
            embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/{filename}").
                WithTitle(prompt.Length > 256 ? prompt.Substring(0, 256) : prompt).
                WithFooter("Generated with majesty-diffusion").
                WithCurrentTimestamp().
                WithFields(fields.ToArray()).
                WithDescription(await GPT3Announce(prompt, guild.Name, channel.Name, user?.Username ?? string.Empty));

            if (user != null)
                embed.WithAuthor(new EmbedAuthorBuilder().WithName(user.Username).WithIconUrl(user.GetDisplayAvatarUrl()));

            var message = MentionUtils.MentionUser(user_id);
            await channel.SendMessageAsync(message, components: builder.Build(), embed: embed.Build());
        }

        private static async Task<string> GPT3Announce(string prompt, string server, string channel, string username)
        {
            try
            {
                var openAI = new OpenAIClient(OpenAIAuthentication.LoadFromEnv());
                var gptPrompt = $"You are NightmareBot, a bot on the {server} Discord server that generates nightmarish art. You have just completed a piece of art titled \"{prompt}\" for the user {username} in the {channel} channel. Write a sarcastic, funny, or weird critique of the piece:";
                var generated = await openAI.CompletionEndpoint.CreateCompletionAsync(gptPrompt, max_tokens: 150, temperature: 0.90, presencePenalty: 0.7, frequencyPenalty: 0.8, engine: new Engine("text-curie-001"));
                var response = generated.Completions.First().Text.Trim().Trim('"');
                if (response.StartsWith(prompt + '"', StringComparison.InvariantCultureIgnoreCase))
                    response = '"' + response;
                if (response.EndsWith('"' + prompt, StringComparison.InvariantCultureIgnoreCase))
                    response += '"';
                return response;
            }
            catch
            {
                return prompt;
            }
        }
    }

}
