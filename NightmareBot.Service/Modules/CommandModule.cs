using Dapr.Client;
using Discord;
using Discord.Interactions;
using LinqToTwitter;
using LinqToTwitter.Common;
using Minio;
using NightmareBot.Handlers;
using NightmareBot.Modals;
using NightmareBot.Models;
using OpenAI;
using System.Text;

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

        [SlashCommand("gptdream", "Generates a nightmare using AI assisted prompt generation", runMode: RunMode.Async)]
        public async Task GptDreamAsync(string prompt)
        {
            await DeferAsync(ephemeral: true);
            var gptPrompt = $"Describe an AI generated artwork with the title \"{prompt}\":\n\n";
            var generated = await _openAI.CompletionEndpoint.CreateCompletionAsync(gptPrompt, max_tokens: 75, temperature: 0.7, presencePenalty: 0, frequencyPenalty: 0, engine: new Engine("text-davinci-002"));
            var newPrompt = generated.Completions.First().Text.Trim();

            var input = new MajestyDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.clip_prompts = input.latent_prompts = new[] { newPrompt };

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(prompt);
            var generatedPromptBytes = Encoding.UTF8.GetBytes(newPrompt);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var gptPromptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/gptprompt.txt").WithStreamData(new MemoryStream(generatedPromptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(gptPromptArgs);

            var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", newPrompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request);

            await ModifyOriginalResponseAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {newPrompt}");

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
                                .AddTextInput("Initial Image", "init_image", value: "", required: false, placeholder: "image url");
                            await RespondWithModalAsync(modalBuilder.Build());
                        }
                        break;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to queue request {ex}");
                await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
                await RespondAsync($"Failed to queue: {ex.Message}", ephemeral: true);
            }
        }

        [ModalInteraction("pixray-simple")]
        public async Task PixraySimpleModalResponse(PixrayModal modal)
        {
            //await DeferAsync(ephemeral: true);
            var input = new PixrayInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<PixrayInput>(Context, input, id);
            if (!string.IsNullOrWhiteSpace(modal.Prompts) && string.IsNullOrWhiteSpace(input.prompts))
                input.prompts = modal.Prompts;

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
            await RespondAsync($"Queued `pixray` dream\n ```{input.settings}```", ephemeral: true);

            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(input.prompts);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/input.yaml").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/yaml");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", input.prompts);
            await Enqueue(request);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `pixray` dream\n ```{input.settings}```"));            
        }

        [ModalInteraction("pixray-advanced")]
        public async Task PixrayAdvancedModalResponse(PixrayModal modal)
        {
            //await DeferAsync(ephemeral: true);
            var input = new PixrayInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<PixrayInput>(Context, input, id);
            
            if (string.IsNullOrWhiteSpace(modal.Settings))
            { 
                await RespondAsync("Settings were not provided.", ephemeral: true);
                return; 
            }
                
            input.settings = modal.Settings;
            await RespondAsync($"Queued `pixray` dream\n ```{input.settings}```", ephemeral: true);

            await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", input.prompts);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `pixray` dream\n ```{input.settings}```"));
            await Enqueue(request);
        }

        [ModalInteraction("latent-diffusion")]
        public async Task LatentDiffusionModalResponse(LatentDiffusionModal modal)
        {
            var input = new LatentDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
            if (!string.IsNullOrWhiteSpace(modal.Prompt) && string.IsNullOrWhiteSpace(input.prompt))
                input.prompt = modal.Prompt;
            if (float.TryParse(modal.Scale, out var scale))
                input.scale = scale;
            if (int.TryParse(modal.Steps, out var steps))
                input.ddim_steps = steps;
            if (int.TryParse(modal.Samples, out var samples))
                input.n_samples = samples;
            await RespondAsync($"Queued `latent-diffusion` dream\n > {input.prompt}", ephemeral: true);

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(modal.Prompt);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);

            await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", input.prompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `latent-diffusion` dream\n > {input.prompt}"));
            await Enqueue(request);            
        }

        [ModalInteraction("majesty-diffusion")]
        public async Task MajestyDiffusionModalResponse(MajestyDiffusionModal modal)
        {
            await RespondAsync($"Queued `majesty-diffusion` dream\n > {modal.Prompt}", ephemeral: true);
            var input = new MajestyDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            if (!string.IsNullOrWhiteSpace(modal.Prompt))
                input.clip_prompts = input.latent_prompts = new []{ modal.Prompt };
            if (!string.IsNullOrWhiteSpace(modal.NegativePrompt))
                input.latent_negatives = modal.NegativePrompt.Split('|', StringSplitOptions.TrimEntries & StringSplitOptions.RemoveEmptyEntries);
            if (!string.IsNullOrWhiteSpace(modal.InitImage))
            {
                input.init_image = modal.InitImage;
                input.init_scale = 0;
                input.init_brightness = 0.5f;
                input.init_noise = 0.8f;
            }

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var settingsBytes = Encoding.UTF8.GetBytes(input.settings);
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(modal.Prompt);
            var idBytes = Encoding.UTF8.GetBytes(id.ToString());
            var putObjectArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putObjectArgs);
            var putSettingsArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(putSettingsArgs);
            var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{id}", modal.Prompt);
            //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
            await Enqueue(request);            
        }

        [ComponentInteraction("enhance-face:*,*")]
        private async Task FaceEnhanceAsync(string id, string image)
        {
            try
            {
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";
                var request = new PredictionRequest<EsrganInput>(Context, new EsrganInput { images = new[] { imageUrl }, face_enhance = true, outscale = 8 }, Guid.NewGuid());
                var prompt = await _daprClient.GetStateAsync<string>("cosmosdb", $"prompts-{id}");
                var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

                var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));                
                var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
                var promptBytes = Encoding.UTF8.GetBytes(prompt.ToString());
                var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
                await _minioClient.PutObjectAsync(putContextArgs);
                var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(idArgs);
                var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(promptArgs);
                var imageArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
                await _minioClient.PutObjectAsync(imageArgs);

                await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{request.id}", prompt);
                await _daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
                await _daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
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
                var prompt = await _daprClient.GetStateAsync<string>("cosmosdb", $"prompts-{id}");
                var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

                var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));                
                var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
                var promptBytes = Encoding.UTF8.GetBytes(prompt.ToString());
                var putContextArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
                await _minioClient.PutObjectAsync(putContextArgs);
                var idArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(idArgs);
                var promptArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
                await _minioClient.PutObjectAsync(promptArgs);
                var imageArgs = new PutObjectArgs().WithBucket("nightmarebot-workflow").WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
                await _minioClient.PutObjectAsync(imageArgs);

                await _daprClient.SaveStateAsync("cosmosdb", $"prompts-{request.id}", prompt);
                await _daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
                await _daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
                await DeferAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling enhance request");
            }
        }

        [ComponentInteraction("pixray_init:*,*")]
        private async Task PixrayInitAsync(string id, string image)
        {
            try
            {
                var prompt = await _daprClient.GetStateAsync<string>("cosmosdb", $"prompts-{id}");

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
                var prompt = await _daprClient.GetStateAsync<string>("cosmosdb", $"prompts-{id}");
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
        private async Task TweetAsync(string id, string image)
        {
            try
            {
                var prompt = await _daprClient.GetStateAsync<string>("cosmosdb", $"prompts-{id}");
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";
                using var httpClient = new HttpClient();
                var imageData = await httpClient.GetByteArrayAsync(imageUrl);


                var upload = await _twitter.UploadMediaAsync(imageData, "image/png", "TweetImage");
                if (upload == null || upload.MediaID == 0)
                {
                    _logger.LogWarning("Twitter upload failed");
                    await RespondAsync("Twitter upload failed", ephemeral: true);
                } 
                else
                {                    
                    var tweet = await _twitter.TweetMediaAsync(prompt, new[] { upload.MediaID.ToString() });
                    if (tweet == null)
                    {
                        _logger.LogWarning("Failed to tweet");
                        await RespondAsync("Failed to tweet", ephemeral: true);
                    }
                    else
                    {
                        await RespondAsync($"https://twitter.com/NightmareBotAI/status/{tweet.ID}");
                    }
                }
            }
            catch (TwitterQueryException ex)
            {
                _logger.LogError(ex, $"Error tweeting: {ex.ReasonPhrase} {ex.Errors} ");
                await RespondAsync($"Unable to tweet image: {ex.ReasonPhrase}", ephemeral: true);
            }
        }

        private async Task Enqueue<T>(PredictionRequest<T> request) where T : IGeneratorInput
        {
            await _daprClient.SaveStateAsync("cosmosdb", $"context-{request.id}", request.context);
            await _daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
            await _daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
        }

    }
}
