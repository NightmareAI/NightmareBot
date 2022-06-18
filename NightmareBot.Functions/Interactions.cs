using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Discord;
using Discord.Rest;
using Discord.Interactions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace NightmareBot.Functions
{
    public class Interactions
    {
        private readonly IServiceProvider _serviceProvider = null;
        private string httpResponse;
        public Interactions(IServiceProvider serviceProvider)
        {            
            _serviceProvider = serviceProvider;
        }      

        [FunctionName("Interact")]        
        public async Task<IActionResult> HandleInteraction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var publicKey = Environment.GetEnvironmentVariable("NIGHTMAREBOT_DISCORD_PUBLIC_KEY");
            var botToken = Environment.GetEnvironmentVariable("NIGHTMAREBOT_DISCORD_TOKEN");            

            var signature = req.Headers["X-Signature-Ed25519"];
            var timestamp = req.Headers["X-Signature-Timestamp"];
            byte[] body = null;            

            if (req.Body != null && req.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    req.Body.CopyTo(ms);
                    body = ms.ToArray();
                }
            }
            else
                body = new byte[0];

            log.LogInformation($"Authenticating with Discord {signature} {timestamp}");
            var discord = new DiscordRestClient();            
            var service = new InteractionService(discord, new InteractionServiceConfig() { RestResponseCallback = this.ResponseCallback });
            await discord.LoginAsync(TokenType.Bot, botToken);

            log.LogInformation("Checking request");

            if (discord.IsValidHttpInteraction(publicKey, signature, timestamp, body))
            {
                log.LogInformation("Processing interaction");
                var interaction = await discord.ParseHttpInteractionAsync(publicKey, signature, timestamp, body);
                
                if (interaction.Type == InteractionType.Ping)
                {

                    var ping = interaction as RestPingInteraction;
                    httpResponse = ping.AcknowledgePing();
                    log.LogInformation($"Responding to ping: {httpResponse}");
                } 
                else
                {                    
                    var context = new RestInteractionContext(discord, interaction);
                    var result = await service.ExecuteCommandAsync(context, _serviceProvider);

                    if (!result.IsSuccess)
                    {
                        switch (result.Error)
                        {
                            case InteractionCommandError.UnmetPrecondition:
                                break;
                            default:
                                break;
                        }
                        log.LogError($"Failed to queue: {result.Error}: {result.ErrorReason}");
                        await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => { await msg.Result.ModifyAsync(p => p.Content = $"Failed to queue: {result.ErrorReason}"); });
                        return new BadRequestResult();
                    }
                }
            }
            return new OkObjectResult(httpResponse);
        }

        public Task ResponseCallback(IInteractionContext context, string responseBody)
        {
            httpResponse = responseBody;
            return Task.FromResult(Task.CompletedTask);
        }

    }
}
