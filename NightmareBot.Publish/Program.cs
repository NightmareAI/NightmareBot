using Azure.Messaging.ServiceBus;
using Discord;
using Discord.Rest;
using Minio;
using NightmareBot.Common;
using OpenAI;
using System.Text.Json;

var discord = new DiscordRestClient();
await discord.LoginAsync(Discord.TokenType.Bot, Environment.GetEnvironmentVariable("NIGHTMAREBOT_TOKEN"));
var openAI = new OpenAIClient(OpenAIAuthentication.LoadFromEnv());

async Task<string> GPT3Announce(string prompt, string server, string channel, string username)
{
    try
    {
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

if (Directory.Exists("/result/pixray"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/result/pixray/context.json"));
    var settings = File.ReadAllText("/result/pixray/input.yaml");
    var prompt = File.ReadAllText("/result/pixray/prompt.txt");
    var id = File.ReadAllText("/result/pixray/id.txt");

    if (context == null)
        return;

    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);

    var builder = new ComponentBuilder();
    builder.WithSelectMenu
        ($"enhance-select-direct:{id},output.png", new List<SelectMenuOptionBuilder>
        {
            new SelectMenuOptionBuilder().WithValue("swinir").WithLabel("SwinIR").WithDescription("Uses SwinIR to upscale 4x"),
            new SelectMenuOptionBuilder().WithValue("esrgan").WithLabel("Real-ESRGAN").WithDescription("Uses Real-ESRGAN to upscale 6x"),
            new SelectMenuOptionBuilder().WithValue("esrgan-face").WithLabel("Real-ESRGAN-Face").WithDescription("Real-ESRGAN+GFPGAN face restoration")
        }, minValues: 1, maxValues: 1, placeholder: "Enhance");
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"dream:{id},output.png").WithLabel("Dream"));
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"pixray-init:{id},output.png").WithLabel("Pixray"));

    var guild = await discord.GetGuildAsync(guild_id);
    if (guild == null)
    {
        Console.WriteLine("Unable to get guild from discord");
        return;
    }
    var channel = await guild.GetTextChannelAsync(channel_id);
    var user = await channel.GetUserAsync(user_id);
    var message = MentionUtils.MentionUser(user_id);
    using var typingState = channel.EnterTypingState();
    var embed = new EmbedBuilder();
    // AddField("video", $"https://dumb.dev/nightmarebot-output/{id}/steps/output.mp4")
    embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/output.png").WithTitle(prompt.Length > 256 ? prompt.Substring(0, 256) : prompt).WithDescription(await GPT3Announce(prompt, guild.Name, channel.Name, user.Username)).WithFooter("Generated by pixray").AddField("settings", settings).WithCurrentTimestamp();

    if (user != null)
        embed.WithAuthor(new EmbedAuthorBuilder().WithName(user.Username).WithIconUrl(user.GetDisplayAvatarUrl()));

    await channel.SendMessageAsync(message, embed: embed.Build(), components: builder.Build());
}

else if (Directory.Exists("/result/swinir"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/result/swinir/context.json"));
    var prompt = File.ReadAllText("/result/swinir/prompt.txt");
    var id = File.ReadAllText("/result/swinir/id.txt");

    if (context == null)
        return;

    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);
       
    var embed = new EmbedBuilder();
    embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/output.png").WithTitle(prompt.Length > 256 ? prompt.Substring(0, 256) : prompt).WithFooter("Enhanced with SwinIR").WithCurrentTimestamp();

    var builder = new ComponentBuilder();
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Danger).WithCustomId($"tweet:{id},output.png").WithLabel("Tweet"));

    var guild = await discord.GetGuildAsync(guild_id);
    if (guild == null)
    {
        Console.WriteLine("Unable to get guild from discord");
        return;
    }

    var channel = await guild.GetTextChannelAsync(channel_id);
    var message = MentionUtils.MentionUser(user_id);
    await channel.SendMessageAsync(message, embed: embed.Build(), components: builder.Build());

}
else if (Directory.Exists("/result/majesty"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/tmp/majesty/context.json"));
    var prompt = File.ReadAllText("/tmp/majesty/prompt.txt");   
    var id = File.ReadAllText("/tmp/majesty/id.txt");

    var images = Directory.GetFiles("/result/majesty", "*.png");
    var filename = Path.GetFileName(images[0]);

    if (context != null)
        await MajestyRespond(id, context, prompt, filename);
}
else if (Directory.Exists("/result/latent-diffusion"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/input/context.json"));
    var prompt = File.ReadAllText("/input/prompt.txt");
    var id = File.ReadAllText("/input/id.txt");

    if (context == null)
        return;    

    var images = Directory.GetFiles("/result/latent-diffusion/samples/", "*.png");

    var builder = new ComponentBuilder();
    List<ActionRowBuilder> actions = new List<ActionRowBuilder>();    
    ActionRowBuilder enhanceMenu = new ActionRowBuilder();
    ActionRowBuilder generateButtons = new ActionRowBuilder();
    ActionRowBuilder pixrayButtons = new ActionRowBuilder();
    var imageOptions = new List<SelectMenuOptionBuilder>();
    var embeds = new List<Embed>();

    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);

    var guild = await discord.GetGuildAsync(guild_id);
    if (guild == null)
    {
        Console.WriteLine("Unable to get guild from discord");
        return;
    }
    var channel = await guild.GetTextChannelAsync(channel_id);
    var user = await channel.GetUserAsync(user_id);

    using var typing = channel.EnterTypingState();    

    for (int ix = 0; ix < images.Length; ix++)
    {
        var filename = Path.GetFileName(images[ix]);
        var embed = new EmbedBuilder().WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/samples/{filename}").WithTitle(prompt).WithFooter($"Sample {ix + 1}").WithCurrentTimestamp();
        if (user != null)
            embed.WithAuthor(new EmbedAuthorBuilder().WithName(user.Username).WithIconUrl(user.GetDisplayAvatarUrl()));
        embeds.Add(embed.Build());
        imageOptions.Add(new SelectMenuOptionBuilder().WithValue($"{ix+1},samples/{filename}").WithLabel($"{ix + 1}"));
        generateButtons.WithButton($"Dream {ix + 1}", $"dream:{id},samples/{filename}", ButtonStyle.Secondary);
        pixrayButtons.WithButton($"Pixray {ix + 1}", $"pixray_init:{id},samples/{filename}", ButtonStyle.Secondary);
    }
    enhanceMenu.WithSelectMenu($"enhance-select-images:{id}", imageOptions, minValues: 1, maxValues: images.Length, placeholder: "Select Images");
    actions.Add(enhanceMenu);
    actions.Add(new ActionRowBuilder().
        WithSelectMenu($"enhance-select-type:{id}", new List<SelectMenuOptionBuilder>
        {
            new SelectMenuOptionBuilder().WithValue("swinir").WithLabel("SwinIR").WithDescription("Uses SwinIR to upscale 4x"),
            new SelectMenuOptionBuilder().WithValue("esrgan").WithLabel("Real-ESRGAN").WithDescription("Uses Real-ESRGAN to upscale 8x"),
            new SelectMenuOptionBuilder().WithValue("esrgan-face").WithLabel("Real-ESRGAN-Face").WithDescription("Real-ESRGAN+GFPGAN face restoration")
        }, minValues: 1, maxValues: 1, placeholder: "Enhance"));
    actions.Add(generateButtons);
    actions.Add(pixrayButtons);
    builder.WithRows(actions);


    var message = MentionUtils.MentionUser(user_id) + "\n" + await GPT3Announce(prompt, guild.Name, channel.Name, user?.Username ?? string.Empty);
    await channel.SendMessageAsync(message, embeds: embeds.ToArray(), components: builder.Build());
}
else if (Directory.Exists("/result/enhance"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/input/context.json"));
    var prompt = File.ReadAllText("/input/prompt.txt");
    var id = File.ReadAllText("/input/id.txt");

    if (context == null)
        return;

    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);

    var message =
        $"> {prompt}\n";

    var file = Path.GetFileName(Directory.GetFiles("/result/enhance").First());

    var embed = new EmbedBuilder();
    string imageUrl = $"https://dumb.dev/nightmarebot-output/{id}/{file}";
    embed.WithImageUrl(imageUrl).WithTitle(prompt.Length > 256 ? prompt.Substring(0, 256) : prompt).WithFooter("Enhanced with Real-ESRGAN").WithFields(new[] { new EmbedFieldBuilder().WithName("URL").WithValue(imageUrl).WithIsInline(false)});

    var builder = new ComponentBuilder();
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Danger).WithCustomId($"tweet:{id},{file}").WithLabel("Tweet"));

    var guild = await discord.GetGuildAsync(guild_id);
    if (guild == null)
    {
        Console.WriteLine("Unable to get guild from discord");
        return;
    }

    var channel = await guild.GetTextChannelAsync(channel_id);
    message += MentionUtils.MentionUser(user_id);
    await channel.SendMessageAsync(message, embed: embed.Build(), components: builder.Build());

}


async Task MajestyRespond(string id, DiscordContext context, string prompt, string filename)
{
    if (context == null)
        return;
    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);

    var builder = new ComponentBuilder();
    builder.WithSelectMenu
        ($"enhance-select-direct:{id},{filename}", new List<SelectMenuOptionBuilder>
        {
            new SelectMenuOptionBuilder().WithValue("swinir").WithLabel("SwinIR").WithDescription("Uses SwinIR to upscale 4x"),
            new SelectMenuOptionBuilder().WithValue("esrgan").WithLabel("Real-ESRGAN").WithDescription("Uses Real-ESRGAN to upscale 8x"),
            new SelectMenuOptionBuilder().WithValue("esrgan-face").WithLabel("Real-ESRGAN-Face").WithDescription("Real-ESRGAN+GFPGAN face restoration")
        }, minValues: 1, maxValues: 1, placeholder: "Enhance");
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"dream:{id},{filename}").WithLabel("Dream"));
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"pixray_init:{id},{filename}").WithLabel("Pixray"));


    var guild = await discord.GetGuildAsync(guild_id);
    if (guild == null)
    {
        Console.WriteLine("Unable to get guild from discord");
        return;
    }

    var channel = await guild.GetTextChannelAsync(channel_id);
    var user = await channel.GetUserAsync(user_id);


    using var typingState = channel.EnterTypingState();
    var embed = new EmbedBuilder();
    embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/{filename}").WithTitle(prompt.Length > 256 ? prompt.Substring(0, 256) : prompt).WithFooter("Generated with majesty-diffusion").WithCurrentTimestamp().WithDescription(await GPT3Announce(prompt, guild.Name, channel.Name, user?.Username ?? string.Empty));

    if (user != null)
        embed.WithAuthor(new EmbedAuthorBuilder().WithName(user.Username).WithIconUrl(user.GetDisplayAvatarUrl()));

    var message = MentionUtils.MentionUser(user_id);
    await channel.SendMessageAsync(message, components: builder.Build(), embed: embed.Build());
}
