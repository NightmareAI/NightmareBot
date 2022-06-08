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

        public CommandModule(DaprClient daprClient, ILogger<CommandModule> logger, CommandHandler handler, TwitterContext twitterContext, MinioClient minioClient, OpenAIClient openAIClient) { _daprClient = daprClient; _logger = logger; _handler = handler; _twitter = twitterContext; _minioClient = minioClient; _openAI = openAIClient; }

        public async Task<string> GetGPTPrompt(string prompt, int max_tokens = 75)
        {
            try
            {
                return await GetGPTResult($"Briefly describe a piece of artwork titled \"{prompt}\":\n\n", prompt);
            }
            catch
            {
                return prompt;
            }
        }

        private async Task<string> GetGPTQueueResponse(string prompt)
        {
            return await GetGPTNotification($"You are NightmareBot, a bot on the {Context.Guild.Name} Discord server that generates nightmarish art. You have just been asked by {Context.User.Username} in the {Context.Channel.Name} channel to generate a piece of art titled ", prompt, ". Say something witty:");
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

        private async Task<string> GetGPTResult(string gptPrompt, string prompt, int max_tokens = 75)
        {
            var generated = await _openAI.CompletionEndpoint.CreateCompletionAsync(gptPrompt, max_tokens: max_tokens, temperature: 0.90, presencePenalty: 0, frequencyPenalty: 0, engine: new Engine("curie-instruct-beta"));
            var response = generated.Completions.First().Text.Trim().Trim('"');
            if (response.StartsWith(prompt + '"', StringComparison.InvariantCultureIgnoreCase))
                response = '"' + response;
            if (response.EndsWith('"' + prompt, StringComparison.InvariantCultureIgnoreCase))
                response += '"';
            if (response.Length > 280)
                response = response.Substring(0, 280);
            return response;
        }

        [SlashCommand("gptdream", "Generates a nightmare using AI assisted prompt generation", runMode: RunMode.Async)]
        public async Task GptDreamAsync(string prompt)
        {
            await DeferAsync();

            var input = new MajestyDiffusionInput();
                input.clip_prompts = input.latent_prompts = new[] { prompt };
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            var newPrompt = await GetGPTPrompt(prompt, 100);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.clip_prompts = new[] { newPrompt };
            
            request.request_state.prompt = newPrompt;
            request.request_state.gpt_prompt = prompt;

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
            await Enqueue(request);

            var response = await GetGPTQueueResponse(prompt);

            await ModifyOriginalResponseAsync(p => p.Content = $"{response}\n *Generated Prompt*\n```{newPrompt}```");

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
        public async Task DreamAsync(
            [Choice("Latent Diffusion (fast, multi image)", "latent-diffusion"), 
                Choice("Majesty Diffusion (latent diffusion fork)", "majesty-diffusion"),
                Choice("Pixray (slow, single image)", "pixray")]
                string dreamer, string prompt)
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
                                .AddTextInput("Negative Prompt", "negative_prompt", value: "low quality image", required: false)
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
            await ModifyOriginalResponseAsync(m => m.Content = $"{response}\n ```{input.settings}```");
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
                input.clip_prompts = new[] { modal.Prompt };
                input.latent_prompts = new[] { gptPrompt };
                request.request_state.prompt = modal.Prompt;
            }            
            if (!string.IsNullOrWhiteSpace(modal.NegativePrompt))
                input.latent_negatives = modal.NegativePrompt.Split('|', StringSplitOptions.TrimEntries & StringSplitOptions.RemoveEmptyEntries);
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
            await Enqueue(request);
            await ModifyOriginalResponseAsync(p => p.Content = $"{response}\n *Generated Prompt*\n```{gptPrompt}```");
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
                await DeferAsync();
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
                await DeferAsync();
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

                        await RealEsrganAsync(id, image, item == "esrgan-face", 6);
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
                            await RealEsrganAsync(id, image, item == "esrgan-face", 6); 
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
                    .AddTextInput("Negative Prompt", "negative_prompt", value: "low quality image", required: false)
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

                        await message.ReplyAsync(await GetGPTNotification("You are NightmareBot, a bot on the HEALTH (the seminal Los Angeles based heavy rock band) Discord chat server that generates nightmarish art based on user prompts. You have just posted a piece titled", prompt, "on Twitter, please write a funny, sarcastic, weird, or creepy one-liner announcement for the channel"));
                    }
                }
            }
            catch (TwitterQueryException ex)
            {
                _logger.LogError(ex, $"Error tweeting: {ex.ReasonPhrase} {ex.Errors} ");
                await message.ReplyAsync($"Unable to tweet image: {ex.ReasonPhrase}");
            }
        }

        private async Task Enqueue<T>(PredictionRequest<T> request) where T : IGeneratorInput, new()
        {
            await _daprClient.SaveStateAsync(Names.StateStore, $"request-{request.id}", request.request_state);
            await _daprClient.SaveStateAsync(Names.StateStore, $"context-{request.id}", request.context);
            await _daprClient.PublishEventAsync(Names.Pubsub, $"request.{request.request_type}", request);
            await _daprClient.SaveStateAsync(Names.StateStore, request.id.ToString(), request);
        }

    }
}
