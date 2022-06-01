using Discord;
using Discord.Rest;
using NightmareBot.Common;


var discord = new DiscordRestClient();
await discord.LoginAsync(Discord.TokenType.Bot, Environment.GetEnvironmentVariable("NIGHTMAREBOT_TOKEN"));


if (Directory.Exists("/result/pixray"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/result/pixray/context.json"));
    var settings = File.ReadAllText("/result/pixray/input.yaml");
    var prompt = File.ReadAllText("/result/pixray/prompt.txt");
    var id = File.ReadAllText("/result/pixray/id.txt");

    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);

    var message =
        $"```{settings}```\n" +
        $"https://dumb.dev/nightmarebot-output/{id}/steps/output.mp4\n";

    var embed = new EmbedBuilder();
    embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/output.png");

    var builder = new ComponentBuilder();
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Primary).WithCustomId($"enhance:{id},output.png").WithLabel("Enhance"));
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Secondary).WithCustomId($"pixray_init:{id},output.png").WithLabel("Dream"));

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
else if (Directory.Exists("/result/swinir"))
{
    var context = System.Text.Json.JsonSerializer.Deserialize<DiscordContext>(File.OpenRead("/result/swinir/context.json"));
    var prompt = File.ReadAllText("/result/swinir/prompt.txt");
    var id = File.ReadAllText("/result/swinir/id.txt");

    ulong.TryParse(context.guild, out var guild_id);
    ulong.TryParse(context.channel, out var channel_id);
    ulong.TryParse(context.message, out var message_id);
    ulong.TryParse(context.user, out var user_id);

    var message =
        $"> {prompt}\n";
        
    var embed = new EmbedBuilder();
    embed.WithImageUrl($"https://dumb.dev/nightmarebot-output/{id}/output.png");

    var builder = new ComponentBuilder();
    builder.WithButton(new ButtonBuilder().WithStyle(ButtonStyle.Danger).WithCustomId($"tweet:{id},output.png").WithLabel("Tweet"));

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