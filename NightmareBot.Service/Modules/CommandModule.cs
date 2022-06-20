using Dapr.Client;
using Discord;
using Discord.Interactions;
using SixLabors.ImageSharp;
using LinqToTwitter;
using LinqToTwitter.Common;
using Minio;
using NightmareBot.Handlers;
using NightmareBot.Modals;
using NightmareBot.Models;
using OpenAI;
using System.Text;
using SixLabors.ImageSharp.Processing;
using NightmareBot.Common;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using System.Diagnostics;
using System.Text.Json;
using NightmareBot.Common.RunPod;
using Microsoft.Azure.Cosmos;

namespace NightmareBot.Modules
{
    public class CommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaprClient _daprClient;
        private readonly CommandHandler _handler;
        private readonly ILogger<CommandModule> _logger;
        private readonly TwitterContext _twitter;
        private readonly MinioClient _minioClient;
        private readonly OpenAIClient _openAI;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly RunPodApiClient _runPodClient;
        private readonly CosmosClient _cosmosClient;

        public CommandModule(DaprClient daprClient, ILogger<CommandModule> logger, CommandHandler handler, TwitterContext twitterContext, MinioClient minioClient, OpenAIClient openAIClient, ServiceBusClient serviceBusClient, RunPodApiClient runPodApiClient, CosmosClient cosmosClient) { _daprClient = daprClient; _logger = logger; _handler = handler; _twitter = twitterContext; _minioClient = minioClient; _openAI = openAIClient; _serviceBusClient = serviceBusClient; _runPodClient = runPodApiClient; _cosmosClient = cosmosClient; }

        public async Task<string> GetGPTPrompt(string prompt, int max_tokens = 64)
        {
            try
            {
                return await GetGPTResult($"Describe the visual style of an image titled \"{prompt}\":\n\n", prompt, max_tokens, 0.9, 2.0, 2.0);
            }
            catch
            {
                return prompt;
            }
        }

        private async Task<string> GetGPTQueueResponse(string prompt, string description = "a piece of art titled")
        {
            return await GetGPTNotification($"You are NightmareBot, a bot on the {Context.Guild.Name} Discord server that generates nightmarish art. You have just been asked by {Context.User.Username} in the {Context.Channel.Name} channel to generate {description} ", prompt, ". What is your response?");
        }

        public async Task<string> GetGPTNotification(string prefix, string prompt, string suffix)
        {
            try
            {
                var response = await GetGPTResult($"{prefix} \"{prompt}\" {suffix}:\n", prompt, 75);

                return response;
            }
            catch
            {
                return prompt;
            }
        }

        private async Task<string> GetGPTResult(string gptPrompt, string prompt, int max_tokens = 75, double temperature = 0.9, double presence = 1.0, double frequency = 1.0, string model="text-curie-001" )
        {
            var generated = await _openAI.CompletionEndpoint.CreateCompletionAsync(gptPrompt, max_tokens: max_tokens, temperature: temperature, presencePenalty: presence, frequencyPenalty: frequency, engine: new Engine("text-curie-001"));
            var response = generated.Completions.First().Text.Trim().Trim('"');
            if (response.StartsWith(prompt + '"', StringComparison.InvariantCultureIgnoreCase))
                response = '"' + response;
            if (response.EndsWith('"' + prompt, StringComparison.InvariantCultureIgnoreCase))
                response += '"';
            if (response.Length > 280)
                response = response.Substring(0, 280);
            return response;
        }

        [SlashCommand("majesty", "Advanced access to majesty-diffusion")]        
        public async Task MajestyAsync(string prompt, LatentDiffusionModelType model = LatentDiffusionModelType.Finetuned)
        {
            var input = new MajestyDiffusionInput();
            input.clip_prompts = input.latent_prompts = new[] { prompt };
            input.latent_diffusion_model = GetModelName(model);
            var modalBuilder = new ModalBuilder()
                .WithTitle("Majesty Diffusion Advanced Mode")
                .WithCustomId("majesty-diffusion-advanced")
                .AddTextInput("Title (does not change prompt, use settings)", "prompt", value: prompt, style: TextInputStyle.Paragraph, required: true)
                .AddTextInput("Settings", "settings", value: input.settings, required: true, style: TextInputStyle.Paragraph);                
            await RespondWithModalAsync(modalBuilder.Build());

        }

        [SlashCommand("nmbstats", "Show stats")]
        public async Task StatsAsync()
        {            
            var container = _cosmosClient.GetDatabase("NightmareBot").GetContainer("statestore");
            var userStats = await container.GetItemQueryStreamIterator($"SELECT value count(s.id) FROM statestore s  WHERE s['value'].context.user = '{Context.User.Id}' AND IS_DEFINED(s['value'].request_type ) AND s['value'].request_type IN ('majesty-diffusion','latent-diffusion','pixray')").ReadNextAsync();
            var guildStats = await container.GetItemQueryStreamIterator($"SELECT value count(s.id) FROM statestore s  WHERE s['value'].context.guild = '{Context.Guild.Id}' AND IS_DEFINED(s['value'].request_type ) AND s['value'].request_type IN ('majesty-diffusion','latent-diffusion','pixray')").ReadNextAsync();
            var globalStats = await container.GetItemQueryStreamIterator($"SELECT value count(s.id) FROM statestore s WHERE IS_DEFINED(s['value'].request_type) AND s['value'].request_type IN ('majesty-diffusion','latent-diffusion','pixray')").ReadNextAsync();

            var message = new StringBuilder();
            message.AppendLine("**Nightmare Stats**");
            message.AppendLine($"Global: {JsonSerializer.Deserialize<JsonElement>(globalStats.Content).GetProperty("Documents")[0]} nightmares");
            message.AppendLine($"{Context.Guild.Name}: {JsonSerializer.Deserialize<JsonElement>(guildStats.Content).GetProperty("Documents")[0]} nightmares");
            message.AppendLine($"{Context.User.Username}: {JsonSerializer.Deserialize<JsonElement>(userStats.Content).GetProperty("Documents")[0]} nightmares");
            await RespondAsync(message.ToString());
        }

        [SlashCommand("nmbqueue", "Show queue status")]
        public async Task QueueCommandAsync()
        {
            await DeferAsync();

            var receiver = _serviceBusClient.CreateReceiver("fast-dreamer");

            var countsByQueue = new Dictionary<string, int>();

            var seq = 0L;
            do
            {
                var batch = await receiver.PeekMessagesAsync(int.MaxValue, seq);
                if (batch.Count > 0)
                {
                    var newSeq = batch[^1].SequenceNumber;
                    if (newSeq == seq)
                        break;
                    

                    foreach (var item in batch)
                    {
                        if (!countsByQueue.ContainsKey(item.SessionId))
                            countsByQueue.Add(item.SessionId, 1);
                        else
                            countsByQueue[item.SessionId]++;
                    }

                    seq = newSeq;
                }
                else
                {
                    break;
                }
            } while (true);


            if (countsByQueue.Count == 0)
            {
                await ModifyOriginalResponseAsync(m => m.Content = "Nothing is in queue");
                return;
            }

            var output = new StringBuilder();
            output.AppendLine("**Queued Nightmares**");
            output.AppendLine("=====================");
            foreach (var item in countsByQueue.OrderBy(c => c.Value))
            {
                if (ulong.TryParse(item.Key, out var userId))
                    output.Append(MentionUtils.MentionUser(userId));
                else
                    output.Append("Unknown user");

                output.AppendLine($": {item.Value}");                
            }
            output.AppendLine($"Total in queue: {countsByQueue.Sum(c => c.Value)}");

            await ModifyOriginalResponseAsync(m => m.Content = output.ToString());
        }

        [SlashCommand("nmblogo", "Logo generation (majesty-diffusion with erlich model)")]
        public async Task LogoCommandAsync(string prompt)
        {
            await DeferAsync();
            var input = new MajestyDiffusionInput();
            input.clip_prompts = new[] { prompt };
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            var newPrompt = await GetGPTResult($"Describe the visual style of a logo for \"{prompt}\":\n\n", prompt, 64, 0.9, 2.0, 2.0);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.latent_prompts = new[] { newPrompt.Replace(": ", "- ") };

            input.latent_diffusion_model = "erlich";
            input.width = 256;
            input.height = 256;

            request.request_state.prompt = newPrompt.Replace(": ", "- ");
            request.request_state.gpt_prompt = prompt.Replace(": ", "- ");

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(prompt);
            var generatedPromptBytes = Encoding.UTF8.GetBytes(newPrompt);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var gptPromptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/gptprompt.txt").WithStreamData(new MemoryStream(generatedPromptBytes)).WithObjectSize(generatedPromptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(gptPromptArgs);

            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", newPrompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request, true);

            var response = await GetGPTQueueResponse(prompt, "a logo for");

            await ModifyOriginalResponseAsync(p => p.Content = $"{response}\n\n *{prompt}*\n```{newPrompt}```");

        }


        public enum LatentDiffusionModelType
        {
            [ChoiceDisplay("Finetuned (default)")]
            Finetuned,
            [ChoiceDisplay("Ongo (paintings)")]            
            Ongo,
            [ChoiceDisplay("Erlich (logos)")]
            Erlich,
            [ChoiceDisplay("Original")]
            Original
        }

        public string GetModelName(LatentDiffusionModelType model)
        {
            switch (model)
            {
                case LatentDiffusionModelType.Original:
                    return "original";                    
                case LatentDiffusionModelType.Ongo:
                    return "ongo";                    
                case LatentDiffusionModelType.Erlich:
                    return "erlich";                    
                case LatentDiffusionModelType.Finetuned:
                default:
                    return "finetuned";                    
            }
        }


        [SlashCommand("nmbart", "Create a work of art")]
        public async Task ArtCommandAsync(string prompt, LatentDiffusionModelType model = LatentDiffusionModelType.Finetuned)
        {
            await DeferAsync();

            var input = new MajestyDiffusionInput();
            input.clip_prompts = input.latent_prompts = new[] { prompt };
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            var newPrompt = await GetGPTPrompt(prompt);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.clip_prompts = new[] { newPrompt.Replace(": ", "- ") };

            input.latent_diffusion_model = GetModelName(model);
            input.width = 320;
            input.height = 384;
            input.custom_schedule_setting = 
            @"[
                [50, 1000, 8],
                'gfpgan:2.0','scale:.9','noise:.75',
                [5,300,4],
            ]";

            request.request_state.prompt = newPrompt.Replace(": ", "- ");
            request.request_state.gpt_prompt = prompt.Replace(": ", "- ");



            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(prompt);
            var generatedPromptBytes = Encoding.UTF8.GetBytes(newPrompt);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var gptPromptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/gptprompt.txt").WithStreamData(new MemoryStream(generatedPromptBytes)).WithObjectSize(generatedPromptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(gptPromptArgs);

            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", newPrompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request, true);

            var response = await GetGPTQueueResponse(prompt);

            await ModifyOriginalResponseAsync(p => p.Content = $"{response}\n\n *{prompt}*\n```{newPrompt}```");
        }


        [SlashCommand("gptdream", "Generates a nightmare using AI assisted prompt generation", runMode: RunMode.Async)]
        public async Task GptDreamAsync(string prompt, LatentDiffusionModelType latent_diffusion_model = LatentDiffusionModelType.Finetuned)
        {
            await DeferAsync();

            var input = new MajestyDiffusionInput();
                input.clip_prompts = input.latent_prompts = new[] { prompt };
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            var newPrompt = await GetGPTPrompt(prompt);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.clip_prompts = new[] { newPrompt.Replace(": ", "- ") };

            input.latent_diffusion_model = GetModelName(latent_diffusion_model);

            request.request_state.prompt = newPrompt.Replace(": ", "- ");
            request.request_state.gpt_prompt = prompt.Replace(": ", "- ");

            

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(prompt);
            var generatedPromptBytes = Encoding.UTF8.GetBytes(newPrompt);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var gptPromptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/gptprompt.txt").WithStreamData(new MemoryStream(generatedPromptBytes)).WithObjectSize(generatedPromptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(gptPromptArgs);

            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", newPrompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request, true);

            var response = await GetGPTQueueResponse(prompt);

            await ModifyOriginalResponseAsync(p => p.Content = $"{response}\n\n *{prompt}*\n```{newPrompt}```");

        }

        [SlashCommand("pixray", "Advanced access to pixray options")]
        public async Task PixrayAsync(string prompt)
        {
            var input = new PixrayInput
            {
                prompts = prompt,
                seed = Random.Shared.Next(int.MaxValue).ToString()
            };

            var modalBuilder = new ModalBuilder()
                .WithTitle("Pixray Dreamer Request")
                .WithCustomId("pixray-advanced")
                .AddTextInput("Settings", "settings", TextInputStyle.Paragraph, required: true, value: input.config);

            await RespondWithModalAsync(modalBuilder.Build());
        }

        [SlashCommand("dream", "Generates a nightmare from a text prompt")]
        public async Task DreamAsync(string prompt,
            [Choice("Majesty Diffusion (default)", "majesty-diffusion"),
            Choice("Latent Diffusion (fast, multi image)", "latent-diffusion"),                 
                Choice("Pixray (slow, single image)", "pixray")]
                string dreamer="majesty-diffusion")
        {
            try
            {
                switch (dreamer)
                {
                    case "latent-diffusion":
                        {
                            var modalBuilder = new ModalBuilder()
                                .WithTitle("Latent-Diffusion Dreamer Request")
                                .WithCustomId("latent-diffusion")
                                .AddTextInput("Prompt", "prompt", value: prompt, style: TextInputStyle.Paragraph, required: true)
                                .AddTextInput("Samples (1-5)", "samples", value: "3", required: false, placeholder: "3")
                                .AddTextInput("Steps (50-200)", "steps", value: "75", required: false, placeholder: "75")
                                .AddTextInput("Diversity Scale (5-20)", "scale", value: "10", required: false, placeholder: "10");

                            await RespondWithModalAsync(modalBuilder.Build());

                        }
                        break;
                    case "pixray":
                        {

                            var modalBuilder = new ModalBuilder()
                             .WithTitle("Pixray Dreamer Request")
                             .WithCustomId("pixray-simple")
                             .AddTextInput("Prompts", "prompts", value: prompt, style: TextInputStyle.Paragraph, required: true)
                             .AddTextInput("Drawer", "drawer", value: "vqgan", placeholder: "vqgan,pixel,clipdraw,line_sketch,super_resolution,vdiff,fft,fast_pixel", required: true)
                             .AddTextInput("Seed", "seed", value: Random.Shared.Next(int.MaxValue).ToString(), required: true)
                             .AddTextInput("Initial Image", "init_image", value: "", placeholder: "image url (png)", required: false);

                            await RespondWithModalAsync(modalBuilder.Build());
                            

                            /*
                            var input = new PixrayInput();
                            var id = Guid.NewGuid();
                            var request = new PredictionRequest<PixrayInput>(Context, input, id);
                            if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(input.prompts))
                                input.prompts = text;
                            await Enqueue(request); */
                        }
                        break;
                    case "majesty-diffusion":
                        {
                            var modalBuilder = new ModalBuilder()
                                .WithTitle("Majesty Diffusion Dreamer Request")
                                .WithCustomId("majesty-diffusion")
                                .AddTextInput("Prompt", "prompt", value: prompt, style: TextInputStyle.Paragraph, required: true)
                                .AddTextInput("Model [finetuned,original,ongo,erlich]", "latent_diffusion_model", value: "finetuned", required: true)
                                .AddTextInput("Image Prompt", "init_image", value: "", required: false, placeholder: "image url");
                            await RespondWithModalAsync(modalBuilder.Build());
                        }
                        break;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to queue request {ex}");                
                await RespondAsync($"Failed to queue: {ex.Message}", ephemeral: true);
            }
        }

        [ModalInteraction("pixray-simple")]
        public async Task PixraySimpleModalResponse(PixrayModal modal)
        {            
            var input = new PixrayInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<PixrayInput>(Context, input, id);
            if (!string.IsNullOrWhiteSpace(modal.Prompts) && string.IsNullOrWhiteSpace(input.prompts))
                input.prompts = modal.Prompts;
            await RespondAsync("Working on it....");
            if (!string.IsNullOrWhiteSpace(modal.InitImage))
            {
                input.init_image = modal.InitImage;
                input.init_image_alpha = 255;
                input.init_noise = "none";
            }
            if (!string.IsNullOrWhiteSpace(modal.Seed))
                input.seed = modal.Seed;
            else
                input.seed = Random.Shared.Next(int.MaxValue).ToString();
            if (!string.IsNullOrWhiteSpace(modal.Drawer))
                input.drawer = modal.Drawer;
            input.settings = input.config;            
            
            request.request_state.prompt = input.prompts;
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(input.prompts);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.yaml").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/yaml");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", input.prompts);
            await Enqueue(request);
            var response = await GetGPTQueueResponse(input.prompts);
            await ModifyOriginalResponseAsync(m => m.Content = $"{response}\n\n *{input.prompts}*\n ```{input.settings}```");
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `pixray` dream\n ```{input.settings}```"));            
        }




        [ModalInteraction("pixray-advanced")]
        public async Task PixrayAdvancedModalResponse(PixrayModal modal)
        {
            //await DeferAsync(ephemeral: true);
            var input = new PixrayInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<PixrayInput>(Context, input, id);
            request.request_state.prompt = input.prompts;
            if (string.IsNullOrWhiteSpace(modal.Settings))
            { 
                await RespondAsync("Settings were not provided.", ephemeral: true);
                return; 
            }
                
            input.settings = modal.Settings;
            await RespondAsync($"Queued `pixray` dream\n ```{input.settings}```", ephemeral: true);

            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", input.prompts);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `pixray` dream\n ```{input.settings}```"));
            await Enqueue(request);
        }

        [ModalInteraction("latent-diffusion")]
        public async Task LatentDiffusionModalResponse(LatentDiffusionModal modal)
        {
            var input = new LatentDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
            await RespondAsync("Working on it....");
            if (!string.IsNullOrWhiteSpace(modal.Prompt) && string.IsNullOrWhiteSpace(input.prompt))
                input.prompt = modal.Prompt;
            if (string.IsNullOrWhiteSpace(input.prompt))
                return;
            if (float.TryParse(modal.Scale, out var scale))
                input.scale = scale;
            if (int.TryParse(modal.Steps, out var steps))
                input.ddim_steps = steps;
            if (int.TryParse(modal.Samples, out var samples))
                input.n_samples = samples;            
            request.request_state.prompt = input.prompt;
            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(modal.Prompt ?? "Missing prompt");
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);

            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", input.prompt);
            var response = await GetGPTQueueResponse(input.prompt);
            await ModifyOriginalResponseAsync(m => m.Content = response);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `latent-diffusion` dream\n > {input.prompt}"));
            await Enqueue(request);            
        }

        [ModalInteraction("majesty-diffusion")]
        public async Task MajestyDiffusionModalResponse(MajestyDiffusionModal modal)
        {
            if (string.IsNullOrWhiteSpace(modal.Prompt))
            {
                await RespondAsync("You didn't fill it in.", ephemeral: true);
                return;
            }
            await RespondAsync("Working on it....");
            var response = await GetGPTQueueResponse(modal.Prompt);
            var input = new MajestyDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            string gptPrompt = "";
            if (!string.IsNullOrWhiteSpace(modal.Prompt))
            {
                gptPrompt = await GetGPTPrompt(modal.Prompt);
                input.clip_prompts = new[] { modal.Prompt.Replace(": ","- ") };
                input.latent_prompts = new[] { gptPrompt.Replace(": ", "- ") };
                request.request_state.prompt = modal.Prompt.Replace(": ", "- ");
            }            
            if (!string.IsNullOrWhiteSpace(modal.InitImage))
            {
                /*
                input.init_image = modal.InitImage;
                input.init_scale = 800;
                input.init_brightness = 0.0f;
                input.init_noise = 0.8f;
                */
                input.image_prompts = new[] { modal.InitImage };
            }
            
            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(modal.Prompt ?? "Missing prompt");
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", modal.Prompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request, true);
            await ModifyOriginalResponseAsync(p => p.Content = $"{response}\n\n *{modal.Prompt}*\n```{gptPrompt}```");
        }

        [ModalInteraction("majesty-diffusion-advanced")]
        public async Task MajestyDiffusionAdvancedModalResponse(MajestyAdvancedModal modal)
        {
            if (string.IsNullOrWhiteSpace(modal.Prompt) || string.IsNullOrWhiteSpace(modal.Settings))
            {
                await RespondAsync("You didn't fill it in.", ephemeral: true);
                return;
            }
            await RespondAsync("Working on it....");
            
            var response = await GetGPTQueueResponse(modal.Prompt);
            var input = new MajestyDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            string gptPrompt = "";
            //if (!string.IsNullOrWhiteSpace(modal.Prompt))
            //{
            //gptPrompt = await GetGPTPrompt(modal.Prompt);
            //input.clip_prompts = new[] { modal.Prompt.Replace(": ", "- ") };
            //input.latent_prompts = new[] { gptPrompt.Replace(": ", "- ") };
            request.request_state.prompt = modal.Prompt.Replace(": ", "- ");
            //}

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(modal.Settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(modal.Prompt ?? "Missing prompt");
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", modal.Prompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request, true);
            await ModifyOriginalResponseAsync(p => { p.Content = $"{response}\n\n *{modal.Prompt}*\n"; p.Attachments = new Optional<IEnumerable<FileAttachment>>(new[] { new FileAttachment(new MemoryStream(settingsBytes), $"{id}.cfg") }); });
        }

        [ComponentInteraction("enhance-face:*,*")]
        private async Task FaceEnhanceAsync(string id, string image)
        {
            await RealEsrganAsync(id, image, true, 4);

        }        

        private async Task RealEsrganAsync(string id, string image, bool faceEnhance, int outscale)
        {
            try
            {
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";
                var request = new PredictionRequest<EsrganInput>(Context, new EsrganInput { images = new[] { imageUrl }, face_enhance = faceEnhance, outscale = outscale }, Guid.NewGuid());
                var prompt = await _daprClient.GetStateAsync<string>(Names.StateStore, $"prompts-{id}");
                var httpClient = new HttpClient();

                try
                {
                    await _minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithCallbackStream(s => { prompt = new StreamReader(s).ReadToEnd(); }));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading prompt from stoage, using state store");
                }

                request.request_state.prompt = prompt;
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

                var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
                var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
                var promptBytes = Encoding.UTF8.GetBytes(prompt.ToString());
                var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
                await _minioClient.PutObjectAsync(putContextArgs);
                var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(idArgs);
                var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(promptArgs);
                var imageArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
                await _minioClient.PutObjectAsync(imageArgs);

                await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{request.id}", prompt);
                await _daprClient.PublishEventAsync(Names.Pubsub, $"request.{request.request_type}", request);
                await _daprClient.SaveStateAsync(Names.StateStore, request.id.ToString(), request);                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling enhance request");
            }

        }

        [ComponentInteraction("enhance:*,*")]
        private async Task EnhanceAsync(string id, string image)
        {
            try
            {
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";
                var request = new PredictionRequest<SwinIRInput>(Context, new SwinIRInput { images = new[] { imageUrl } }, Guid.NewGuid());
                var prompt = await _daprClient.GetStateAsync<string>(Names.StateStore, $"prompts-{id}");
                var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);                
                try
                {
                    await _minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithCallbackStream(s => { prompt = new StreamReader(s).ReadToEnd(); }));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading prompt from stoage, using state store");
                }
                request.request_state.prompt = prompt;
                var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));                
                var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
                var promptBytes = Encoding.UTF8.GetBytes(prompt.ToString());
                var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
                await _minioClient.PutObjectAsync(putContextArgs);
                var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(idArgs);
                var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(promptArgs);
                var imageArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
                await _minioClient.PutObjectAsync(imageArgs);

                await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{request.id}", prompt);
                await _daprClient.PublishEventAsync(Names.Pubsub, $"request.{request.request_type}", request);
                await _daprClient.SaveStateAsync(Names.StateStore, request.id.ToString(), request);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling enhance request");
            }
        }

        [ComponentInteraction("enhance-select-images:*")]
        private async Task EnhanceSelectAsync(string id, string[] selected)
        {
            //var state = await _daprClient.GetStateAsync<RequestState>("cosmosdb", $"request-{id}");
            /*
            if (state?.response_images == null)
            {                
                await RespondAsync("Request state not found. Sorry :(");
                return;
            }
            var count = state.response_images.Count();
            for (int i = 0; i < count; i++)
                state.response_images.ElementAt(i).selected = selected.Contains(i.ToString());
            */            
            await _daprClient.SaveStateAsync(Names.StateStore, $"enhance-selected-{Context.User.Id}-{id}", selected);
            await DeferAsync();
        }

        [ComponentInteraction("enhance-select-direct:*,*")]
        private async Task EnhanceSelectDirectAsync(string id, string image, string[] selected)
        {
            var item = selected.FirstOrDefault();
            if (item == null) { return; }
            await DeferAsync();
            switch (item)
            {
                case "swinir":

                        await EnhanceAsync(id, image);
                    break;
                case "esrgan":
                case "esrgan-face":

                        await RealEsrganAsync(id, image, item == "esrgan-face", 8);
                    break;
            }

            var message = await GetOriginalResponseAsync();
            var newComponents = new ComponentBuilder();
            var customId = $"enhance-select-direct:{id},{image}";            
            foreach (var component in message.Components)
            {
                if (component is SelectMenuComponent menuComponent)
                {
                    if (menuComponent.CustomId == customId)
                    {
                        _logger.LogInformation($"Found {customId}");
                        var newMenuComponent = new SelectMenuBuilder().WithCustomId(customId).WithMinValues(menuComponent.MinValues).WithMaxValues(menuComponent.MaxValues).WithPlaceholder(menuComponent.Placeholder);
                        foreach (var option in menuComponent.Options)
                            if (option.Value != item)
                                newMenuComponent.AddOption(new SelectMenuOptionBuilder(option));
                        newComponents.WithSelectMenu(newMenuComponent);
                    }
                    else newComponents.WithSelectMenu(new SelectMenuBuilder(menuComponent));
                }
                else if (component is ButtonComponent buttonComponent)
                    newComponents.WithButton(new ButtonBuilder(buttonComponent));
                    
            }
            var newContent = message.Content + $"\n*Enhance using {item} has been requested*";
            await message.ModifyAsync(m => { m.Content = newContent; });
        }

        [ComponentInteraction("enhance-select-type:*")]
        private async Task EnhanceSelectTypeAsync(string id, string[] selected)
        {
            var state = await _daprClient.GetStateAsync<string[]>(Names.StateStore, $"enhance-selected-{Context.User.Id}-{id}");
            /*
            if (state?.response_images == null || state?.request_id == null)
            {
                await RespondAsync("Request state not found. Sorry :(");
                return;
            }
            */
            var item = selected.FirstOrDefault();
            if (item == null) { await RespondAsync(); return; }            
            /*
            List<ResponseImage> images = new List<ResponseImage>();
            
            for (int i = 0; i < state.response_images.Count(); i++)
            {
                var image = state.response_images.ElementAt(i);
                if (image.selected)
                {
                    images.Add(image);
                    imageIds += $"{i + 1} ";
                }
            }
            imageIds = imageIds.Trim();
            */
            if (state == null || state.Length == 0) { await RespondAsync("You must select which images you'd like enhanced!", ephemeral: true); return; }
            var imageIds = new List<string>();
            var images = new List<string>();            
            foreach (var image in state)
            {
                var sp = image.Split(',');
                imageIds.Add(sp[0]);
                images.Add(sp[1]);
            }
            await DeferAsync();            

            switch (item)
            {
                case "swinir":
                    foreach (var image in images)
                        await EnhanceAsync(id, image);
                    break;
                case "esrgan":                    
                case "esrgan-face":                    
                        foreach (var image in images)
                            await RealEsrganAsync(id, image, item == "esrgan-face", 8); 
                    break;
            }

            var message = await GetOriginalResponseAsync();            
            var newContent = message.Content + $"\n*Enhance using {item} has been requested for images {string.Join(',', imageIds)}*";
            await message.ModifyAsync(m => m.Content = newContent);
        }

        [ComponentInteraction("pixray_init:*,*")]
        private async Task PixrayInitAsync(string id, string image)
        {
            try
            {
                var prompt = await _daprClient.GetStateAsync<string>(Names.StateStore, $"prompts-{id}");

                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";

                var modalBuilder = new ModalBuilder()
                    .WithTitle("Pixray Dreamer Request")
                    .WithCustomId("pixray-simple")
                    .AddTextInput("Prompts", "prompts", value: prompt, style: TextInputStyle.Paragraph, required: true)
                    .AddTextInput("Drawer", "drawer", value: "vqgan", placeholder: "vqgan,pixel,clipdraw,line_sketch,super_resolution,vdiff,fft,fast_pixel", required: true)
                    .AddTextInput("Seed", "seed", value: Random.Shared.Next(int.MaxValue).ToString(), required: true)
                    .AddTextInput("Initial Image", "init_image", value: imageUrl, placeholder: "image url (png)", required: false);

                await RespondWithModalAsync(modalBuilder.Build());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dream request");
            }

        }

        [ComponentInteraction("dream:*,*")]
        private async Task DreamButtonAsync(string id, string image)
        {
            try
            {
                var prompt = await _daprClient.GetStateAsync<string>(Names.StateStore, $"prompts-{id}");
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";

                var modalBuilder = new ModalBuilder()
                    .WithTitle("Majesty Diffusion Dreamer Request")
                    .WithCustomId("majesty-diffusion")
                    .AddTextInput("Prompt", "prompt", value: prompt, style: TextInputStyle.Paragraph, required: true)
                    .AddTextInput("Negative Prompt", "negative_prompt", placeholder: "optional (things to avoid)", required: false)
                    .AddTextInput("Initial Image", "init_image", value: imageUrl, required: false, placeholder: "image url");
                await RespondWithModalAsync(modalBuilder.Build());

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dream request");
            }
        }
        [ComponentInteraction("tweet:*,*")]
        private async Task TweetAsync(string id, string file)
        {
            await DeferAsync();
            var message = await GetOriginalResponseAsync();
            
            try
            {
                string prompt = await _daprClient.GetStateAsync<string>(Names.StateStore, $"prompts-{id}");
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{file}";
                using var httpClient = new HttpClient();
                var imageData = await httpClient.GetByteArrayAsync(imageUrl);

                while (imageData.Length > 5 * 1024 * 1024)
                {
                    var image = SixLabors.ImageSharp.Image.Load(imageData);
                    var newHeight = image.Height / 2;
                    var newWidth = image.Width / 2;
                    image.Mutate(o => o.Resize(newWidth, newHeight));
                    using var stream = new MemoryStream();
                    await image.SaveAsPngAsync(stream);
                    imageData = stream.ToArray();
                }

                var promptArgs = new GetObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{id}/prompt.txt").WithCallbackStream(s => { prompt = new StreamReader(s).ReadToEnd(); });
                try
                {
                    await _minioClient.GetObjectAsync(promptArgs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading prompt from stoage, using state store");
                }

                if (prompt.Length > 280)
                    prompt = prompt.Substring(0, 280);

                var upload = await _twitter.UploadMediaAsync(imageData, "image/png", "TweetImage");
                if (upload == null || upload.MediaID == 0)
                {
                    _logger.LogWarning("Twitter upload failed");
                    await message.ReplyAsync("Twitter upload failed");
                } 
                else
                {                                        
                    var tweet = await _twitter.TweetMediaAsync(prompt, new[] { upload.MediaID.ToString() });
                    if (tweet == null)
                    {
                        _logger.LogWarning("Failed to tweet");
                        await message.ReplyAsync("Twitter upload failed");
                    }
                    else
                    {                                                
                        await message.ModifyAsync(m => { m.Components = null; m.Content = message.Content + $"\nhttps://twitter.com/NightmareBotAI/status/{tweet.ID}"; });

                        await message.ReplyAsync(await GetGPTNotification($"You are NightmareBot, a bot on the {Context.Guild.Name} Discord server that generates nightmarish art based on user prompts. You have just posted a piece titled", prompt, $"on Twitter, please write a funny, sarcastic, weird, or creepy announcement for the {Context.Channel.Name} channel"));
                    }
                }
            }
            catch (TwitterQueryException ex)
            {
                _logger.LogError(ex, $"Error tweeting: {ex.ReasonPhrase} {ex.Errors} ");
                await message.ReplyAsync($"Unable to tweet image: {ex.ReasonPhrase}");
            }
        }

        private async Task<string> ExecuteVastClient(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = @"python", Arguments = $"C:\\Users\\palp\\bin\\vast.py {args} --raw" };
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            var proc = new Process { StartInfo = startInfo };
            proc.Start();
            return await proc.StandardOutput.ReadToEndAsync();
        }

        private async Task Enqueue<T>(PredictionRequest<T> request, bool runpod=false) where T : IGeneratorInput, new()
        {
            await _daprClient.SaveStateAsync(Names.StateStore, $"request-{request.id}", request.request_state);
            await _daprClient.SaveStateAsync(Names.StateStore, $"context-{request.id}", request.context);
            
            if (runpod)
            {
                // Do this somewhere better
                var pods = await _runPodClient.GetPodsWithClouds();
                foreach (var p in pods) { _logger.LogInformation($"{p.Key.Id}({p.Key.DesiredStatus}) : {p.Value.GpuName} {p.Value.MinimumBidPrice}"); };
                if (pods.Keys.Any(p => p.DesiredStatus != "RUNNING"))
                {
                    foreach (var pod in  pods.OrderBy(p => p.Value.MinimumBidPrice).Where(p => p.Value.MinimumBidPrice != null && p.Value.MinimumBidPrice > 0 && p.Key.DesiredStatus != "RUNNING"))
                    {
                        try
                        {
                            if (pod.Value.MinimumBidPrice.HasValue)
                            {
                                var startResult = await _runPodClient.StartSpotPod(pod.Key.Id, pod.Value.MinimumBidPrice.Value + .01f);
                                _logger.LogInformation($"Started pod {pod.Key.Id} at ${pod.Value.MinimumBidPrice + 0.01f}/hr: {startResult}");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to start pod {pod.Key.Id} at ${pod.Value.MinimumBidPrice + .01f}/hr");
                        }
                    }                    
                }

                var sender = _serviceBusClient.CreateSender("fast-dreamer");
                await sender.SendMessageAsync(new ServiceBusMessage(request.id.ToString()) { SessionId = Context.User.Id.ToString() });

            }
            else
            {
                await _daprClient.PublishEventAsync(Names.Pubsub, $"request.{request.request_type}", request);
            }
            
            await _daprClient.SaveStateAsync(Names.StateStore, request.id.ToString(), request);
        }

    }
}