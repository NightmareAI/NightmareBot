using Azure.Messaging.ServiceBus;
using Dapr.Client;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Minio;
using NightmareBot.Common;
using NightmareBot.Common.RunPod;
using NightmareBot.Models;
using OpenAI;
using System.Text;

namespace NightmareBot.Modules;

public class GenerateModel : ModuleBase<SocketCommandContext>
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<GenerateModel> _logger;
    private readonly InteractionService _handler;
    private readonly MinioClient _minioClient;
    private readonly OpenAIClient _openAI;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly RunPodApiClient _runPodClient;

    public GenerateModel(DaprClient daprClient, InteractionService handler, MinioClient minioClient, OpenAIClient openAI, ILogger<GenerateModel> logger, ServiceBusClient serviceBusClient, RunPodApiClient runPodApiClient)
    {        
        _daprClient = daprClient;
        _handler = handler;
        this._minioClient = minioClient;
        _openAI = openAI;
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _runPodClient = runPodApiClient;
    }

    [Command("reg")]
    [Discord.Commands.Summary("Registers guild commands")]
    public async Task Register()
    {
        try
        {
            await _handler.RegisterCommandsToGuildAsync(Context.Guild.Id);
        }
        catch(Exception exception)
        {
            _logger.LogError(exception, exception.Message);
        }

        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("gen")]
    [Discord.Commands.Summary("Generates a nightmare using the default model (currently latent diffusion, or pixray if an image is supplied).")]
    public async Task GenerateAsync([Remainder] [Discord.Commands.Summary("The prediction text")] string text)
    {
        if (Context.Message.Attachments.Any() || (Context.Message.ReferencedMessage?.Attachments.Any() ?? false)) 
        {
            var input = new MajestyDiffusionInput();
            input.clip_prompts = input.latent_prompts = new[] { text };
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            var newPrompt = await GetGPTPrompt(text);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.clip_prompts = new[] { newPrompt };
            request.request_state.prompt = text;
            request.request_state.gpt_prompt = newPrompt;

            await MajestyAsync(request);
        } 
        else 
        {
            var input = new LatentDiffusionInput();    
            await LatentDiffusionAsync(text, input);  
        }        
    }

    public async Task<string> GetGPTPrompt(string prompt)
    {
        try
        {
            var gptPrompt = $"Briefly describe a piece of artwork titled \"{prompt}\":\n\n";
            var generated = await _openAI.CompletionEndpoint.CreateCompletionAsync(gptPrompt, max_tokens: 64, temperature: 0.7, presencePenalty: 0, frequencyPenalty: 0, engine: new Engine("text-curie-001"));
            return generated.Completions.First().Text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to query GPT-3 for {prompt}");
            return String.Empty;
        }
    }


    public async Task MajestyAsync(PredictionRequest<MajestyDiffusionInput> request)
    {
        List<IAttachment> imagePrompts = new List<IAttachment>();        
        if (Context.Message.Attachments != null)
            imagePrompts.AddRange(Context.Message.Attachments);
        if (Context.Message.ReferencedMessage?.Attachments != null)
            imagePrompts.AddRange(Context.Message.ReferencedMessage.Attachments);
        if (imagePrompts.Any())
        {
            request.input.image_prompts = imagePrompts.Select(i => i.Url).ToArray();
        }

        var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.input));
        var settingsBytes = Encoding.UTF8.GetBytes(request.input.settings);
        var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
        var promptBytes = Encoding.UTF8.GetBytes(request.request_state.prompt ?? "Missing prompt");
        var generatedPromptBytes = Encoding.UTF8.GetBytes(request.request_state.gpt_prompt ?? "Missing prompt");
        var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
        var putObjectArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/input.json").WithStreamData(new MemoryStream(inputBytes)).WithObjectSize(inputBytes.Length).WithContentType("application/json");
        await _minioClient.PutObjectAsync(putObjectArgs);
        var putSettingsArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/settings.cfg").WithStreamData(new MemoryStream(settingsBytes)).WithObjectSize(settingsBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(putSettingsArgs);
        var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
        await _minioClient.PutObjectAsync(putContextArgs);
        var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(promptArgs);
        var gptPromptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/gptprompt.txt").WithStreamData(new MemoryStream(generatedPromptBytes)).WithObjectSize(generatedPromptBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(gptPromptArgs);

        var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
        await _minioClient.PutObjectAsync(idArgs);
        await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{request.id}", request.request_state.prompt);
        //await GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync(p => p.Content = $"Queued `majesty-diffusion` dream\n > {modal.Prompt}"));
        await Enqueue(request, true);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));
    }

    [Command("gptgen")]
    public async Task AiGenerateAsync([Remainder][Discord.Commands.Summary("The prediction text")] string text)
    {
        if (Context.Message.Attachments.Any() || (Context.Message.ReferencedMessage?.Attachments.Any() ?? false))
        {
            var input = new MajestyDiffusionInput();
            input.clip_prompts = input.latent_prompts = new[] { text };
            var id = Guid.NewGuid();
            var request = new PredictionRequest<MajestyDiffusionInput>(Context, input, id);
            var newPrompt = await GetGPTPrompt(text);
            if (!string.IsNullOrWhiteSpace(newPrompt))
                input.clip_prompts = new[] { text };
            request.request_state.prompt = text;
            request.request_state.gpt_prompt = newPrompt;

            await MajestyAsync(request);
        }
        else
        {
            //var prompt = $"Describe an AI generated artwork with the title \"{text}\":\n\n";
            var newPrompt = await GetGPTPrompt(text);
            var input = new LatentDiffusionInput();
            var id = Guid.NewGuid();
            var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
            input.prompt = newPrompt;
            request.request_state.prompt = newPrompt;
            request.request_state.gpt_prompt = text;

            var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var promptBytes = Encoding.UTF8.GetBytes(input.prompt);
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
            //_generateService.LatentDiffusionQueue.Enqueue(request);
            await Enqueue(request);
            await Context.Message.AddReactionAsync(new Emoji("✔️"));
        }
    }

    [Command("ldm")]
    [Discord.Commands.Summary("Generates a nightmare using the latent diffusion model")]
    public async Task LatentDiffusionAsync([Discord.Commands.Summary("The prediction text")] string text, [Discord.Commands.Summary("Latent diffusion settings")] LatentDiffusionInput input) 
    {
        var id = Guid.NewGuid();
        var request = new PredictionRequest<LatentDiffusionInput>(Context, input, id);
        if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(input.prompt))
            input.prompt = text;
        request.request_state.prompt = text;

        var inputBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(input));
        var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
        var promptBytes = Encoding.UTF8.GetBytes(input.prompt ?? "Missing prompt");
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
        //_generateService.LatentDiffusionQueue.Enqueue(request);
        await Enqueue(request);
        await Context.Message.AddReactionAsync(new Emoji("✔️"));        
    }

    [Command("pixray")]
    [Discord.Commands.Summary("Raw access to the Pixray engine")]
    public async Task PixrayAsync([Discord.Commands.Summary("The prediction text")] string text, [Discord.Commands.Summary("Extra Pixray settings")] PixrayInput input) 
    {
        var id = Guid.NewGuid();
        var request = new PredictionRequest<PixrayInput>(Context, input, id);
        if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(input.prompts))
            input.prompts = text;

        IAttachment? initImage = null;        
        if (Context.Message.Attachments.Any()) {
            // First attachment is init image, that's all for now
            initImage = Context.Message.Attachments.First();            
        } else if (Context.Message.ReferencedMessage?.Attachments.Any() ?? false)
        {
            initImage = Context.Message.ReferencedMessage.Attachments.First();            
        }

        if (initImage != null)
        {
            request.input.init_image = initImage.Url;
            if (initImage.Width.HasValue && initImage.Height.HasValue) 
            {
                double width = initImage.Width.Value;
                double height = initImage.Height.Value;

                while (width > 1024 || height > 1024)
                {
                    width = width *0.75;
                    height = height *0.75;
                }

                request.input.size = new int[] { (int)width, (int)height };
            }
        }

        await _daprClient.SaveStateAsync(Names.StateStore, $"prompts-{id}", input.prompts);
        await Enqueue(request);        
        await Context.Message.AddReactionAsync(new Emoji("✔️"));        
    }

    [Command("fenhance")]
    public async Task FaceEnhanceAsync()
    {
        var images = new List<string>();
        var id = Guid.NewGuid();

        foreach (var attachment in Context.Message.Attachments)
        {
            images.Add(attachment.Url);
        }

        if (Context.Message.ReferencedMessage != null)
        {
            foreach (var attachment in Context.Message.ReferencedMessage.Attachments)
            {
                images.Add(attachment.Url);
            }
        }

        var image = images.FirstOrDefault();

        if (image != null)
        {
            var httpClient = new HttpClient();
            var input = new EsrganInput { images = images.ToArray(), face_enhance = true, outscale = 2 };
            var request = new PredictionRequest<EsrganInput>(Context, input, id);
            var imageBytes = await httpClient.GetByteArrayAsync(image);
            request.request_state.prompt = "Enhanced image";
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
            var promptBytes = Encoding.UTF8.GetBytes("Enhanced image");
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var imageArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
            await _minioClient.PutObjectAsync(imageArgs);

            await Enqueue(request);
            //_generateService.SwinIRRequestQueue.Enqueue(request);
            await Context.Message.AddReactionAsync(new Emoji("✔️"));
        }
    }

    [Command("enhance")]
    public async Task EnhanceAsync()
    {
        var images = new List<string>();
        var id = Guid.NewGuid();

        foreach (var attachment in Context.Message.Attachments) 
        {
            images.Add(attachment.Url);
        }

        if (Context.Message.ReferencedMessage != null)
        {
            foreach (var attachment in Context.Message.ReferencedMessage.Attachments)
            {
                images.Add(attachment.Url);
            }
        }

        var image = images.FirstOrDefault();

        if (image != null) {
            var httpClient = new HttpClient();
            var input = new SwinIRInput { images = images.ToArray() };
            var request = new PredictionRequest<SwinIRInput>(Context, input, id);
            var imageBytes = await httpClient.GetByteArrayAsync(image);
            request.request_state.prompt = "Enhanced image";
            var contextBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(request.context));
            var idBytes = Encoding.UTF8.GetBytes(request.id.ToString());
            var promptBytes = Encoding.UTF8.GetBytes("Enhanced image");
            var putContextArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/context.json").WithStreamData(new MemoryStream(contextBytes)).WithObjectSize(contextBytes.Length).WithContentType("application/json");
            await _minioClient.PutObjectAsync(putContextArgs);
            var idArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/id.txt").WithStreamData(new MemoryStream(idBytes)).WithObjectSize(idBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(idArgs);
            var promptArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/prompt.txt").WithStreamData(new MemoryStream(promptBytes)).WithObjectSize(promptBytes.Length).WithContentType("text/plain");
            await _minioClient.PutObjectAsync(promptArgs);
            var imageArgs = new PutObjectArgs().WithBucket(Names.WorkflowBucket).WithObject($"{request.id}/input.png").WithStreamData(new MemoryStream(imageBytes)).WithObjectSize(imageBytes.Length).WithContentType("image/png");
            await _minioClient.PutObjectAsync(imageArgs);

            await Enqueue(request);
            //_generateService.SwinIRRequestQueue.Enqueue(request);
            await Context.Message.AddReactionAsync(new Emoji("✔️"));
        }
    }

    private async Task Enqueue<T>(PredictionRequest<T> request, bool runpod = false) where T : IGeneratorInput, new()
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
                foreach (var pod in pods.OrderBy(p => p.Value.MinimumBidPrice).Where(p => p.Value.MinimumBidPrice != null && p.Value.MinimumBidPrice > 0 && p.Key.DesiredStatus != "RUNNING"))
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