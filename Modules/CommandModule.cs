using Dapr.Client;
using Discord;
using Discord.Interactions;
using NightmareBot.Handlers;
using NightmareBot.Modals;
using NightmareBot.Models;

namespace NightmareBot.Modules
{
    public class CommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaprClient _daprClient;
        private readonly CommandHandler _handler;
        private readonly ILogger<CommandModule> _logger;
        public InteractionService Commands { get; set; }

        public CommandModule(DaprClient daprClient, ILogger<CommandModule> logger, CommandHandler handler) { _daprClient = daprClient; _logger = logger; _handler = handler; }

        [SlashCommand("dream", "Generates a nightmare from a text prompt")]
        public async Task DreamAsync(
            [Choice("Latent Diffusion (fast, multi image)", "latent-diffusion"), 
                Choice("Pixray (slow, single image)", "pixray")]
                string dreamer, 
            [Choice("Simple", "simple"), Choice("Advanced", "advanced")] 
                string mode, 
        
            string prompt)
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
                                .AddTextInput("Prompt", "prompt", value: prompt, style: TextInputStyle.Paragraph, required: true);

                            await RespondWithModalAsync(modalBuilder.Build());

                        }
                        break;
                    case "pixray":
                        {

                            var input = new PixrayInput
                            {
                                prompts = prompt,
                                seed = Random.Shared.Next(int.MaxValue).ToString()
                            };
                            
                            switch (mode)
                            {
                                case "simple":
                                    {
                                        /*
                                        var engineRow = new ActionRowBuilder().WithSelectMenu(new SelectMenuBuilder()
                                            .WithCustomId("engine")
                                            .AddOption("vqgan", "vqgan")
                                            .AddOption("diffusion", "diffusion")
                                            .AddOption("pixel", "pixel")
                                            .AddOption("clipdraw", "clipdraw"));
                                        */
                                        var modalBuilder = new ModalBuilder()
                                            .WithTitle("Pixray Dreamer Request")
                                            .WithCustomId("pixray-simple")
                                            .AddTextInput("Prompts", "prompts", value: prompt, style: TextInputStyle.Paragraph, required: true)
                                            .AddTextInput("Drawer", "drawer", value: "vqgan", placeholder: "(vqgan, pixel, clipdraw, line_sketch, super_resolution, vdiff, fft, fast_pixel)", required: true)
                                            .AddTextInput("Seed", "seed", value: Random.Shared.Next(int.MaxValue).ToString(), required: true)
                                            .AddTextInput("Initial Image", "init_image", value: "", placeholder: "image url (png)", required: false);

                                        await RespondWithModalAsync(modalBuilder.Build());

                                    }
                                    break;
                                case "advanced":
                                    {
                                        var modalBuilder = new ModalBuilder()
                                            .WithTitle("Pixray Dreamer Request")
                                            .WithCustomId("pixray-advanced")
                                            .AddTextInput("Settings", "settings", TextInputStyle.Paragraph, required: true, value: input.config);
                                        await RespondWithModalAsync(modalBuilder.Build());
                                    }
                                    break;
                            }
                            

                            /*
                            var input = new PixrayInput();
                            var id = Guid.NewGuid();
                            var request = new PredictionRequest<PixrayInput>(Context, input, id);
                            if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(input.prompts))
                                input.prompts = text;
                            await Enqueue(request); */
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

            if (!string.IsNullOrWhiteSpace(modal.InitImage))
                input.init_image = modal.InitImage;
            if (!string.IsNullOrWhiteSpace(modal.InitImage))
                input.init_image = modal.InitImage;
            if (!string.IsNullOrWhiteSpace(modal.Seed))
                input.seed = modal.Seed;
            else
                input.seed = Random.Shared.Next(int.MaxValue).ToString();
            input.settings = input.config;

            await Enqueue(request);
            await RespondAsync($"Queued `pixray` dream\n > {input.settings}", ephemeral: true);
        }

        [ModalInteraction("pixray-advanced")]
        public async Task PixrayAdvancedModalResponse(PixrayModal modal)
        {
            var input = new PixrayInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<PixrayInput>(Context, input, id);
            
            if (string.IsNullOrWhiteSpace(modal.Settings))
            { 
                await RespondAsync("Settings were not provided.", ephemeral: true);
                return; 
            }
                
            input.settings = modal.Settings;

            await Enqueue(request);
            await RespondAsync($"Queued `pixray` dream\n > {input.settings}", ephemeral: true);
        }

        [ModalInteraction("latent-diffusion")]
        public async Task LatentDiffusionModalResponse(LatentDiffusionModal modal)
        {
            var input = new LatentDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
            if (!string.IsNullOrWhiteSpace(modal.Prompt) && string.IsNullOrWhiteSpace(input.prompt))
                input.prompt = modal.Prompt;
            await Enqueue(request);
            await RespondAsync($"Queued `latent-diffusion` dream\n > {input.prompt}", ephemeral: true);
        }


        [ComponentInteraction("enhance:*,*")]
        private async Task EnhanceAsync(string id, string image)
        {
            try
            {
                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";
                var request = new PredictionRequest<SwinIRInput>(Context, new SwinIRInput { images = new[] { imageUrl } }, Guid.NewGuid());

                using var daprClient = new DaprClientBuilder().Build();
                await daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
                await daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
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
                var request =
                await _daprClient.GetStateAsync<PredictionRequest<LatentDiffusionInput>>("cosmosdb", id);

                var imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{image}";

                var input = new PixrayInput();
                input.prompts = request.input.prompt;
                input.seed = request.id.ToString();
                input.init_image = imageUrl;
                input.init_image_alpha = 255;
                input.init_noise = "none";

                var modalBuilder = new ModalBuilder()
                    .WithTitle("Pixray Dreamer Request")
                    .WithCustomId("pixray-advanced")
                    .AddTextInput("Settings", "settings", TextInputStyle.Paragraph, required: true, value: input.config);

                await RespondWithModalAsync(modalBuilder.Build());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dream request");
            }

        }

        private async Task Enqueue<T>(PredictionRequest<T> request) where T : IGeneratorInput
        {
            await _daprClient.PublishEventAsync("jetstream-pubsub", $"request.{request.request_type}", request);
            await _daprClient.SaveStateAsync("cosmosdb", request.id.ToString(), request);
        }

    }
}
