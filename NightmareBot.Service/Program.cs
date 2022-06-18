using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using LinqToTwitter.OAuth;
using Microsoft.Azure.Cosmos;
using NightmareBot;
using NightmareBot.Common.RunPod;
using NightmareBot.Handlers;
using OpenAI;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOpenTelemetryTracing(x =>
{
    x.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("NightmareBot"));
    x.AddAspNetCoreInstrumentation();
    x.AddHttpClientInstrumentation();
    //x.AddConsoleExporter();
});

builder.Services.AddSingleton<BotLogger>();
builder.Services.AddControllers().AddDapr();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(x => new DiscordSocketClient(new DiscordSocketConfig() {GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers, AlwaysDownloadUsers = true, UseInteractionSnowflakeDate = false}));
builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig() { DefaultRunMode = Discord.Interactions.RunMode.Async, UseCompiledLambda = true })) ;
builder.Services.AddSingleton<CommandService>();
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton(x => new OpenAIClient(OpenAIAuthentication.LoadFromEnv()));
builder.Services.AddSingleton(x => new ServiceBusClient(Environment.GetEnvironmentVariable("NIGHTMAREBOT_SERVICEBUS_CONNECTION_STRING")));
builder.Services.AddSingleton(x => new RunPodApiClient(Environment.GetEnvironmentVariable("NIGHTMAREBOT_RUNPOD_KEY") ?? String.Empty));
builder.Services.AddSingleton(x => new CosmosClient(Environment.GetEnvironmentVariable("NIGHTMAREBOT_COSMOSDB_CONNECTION_STRING")));
//builder.Services.AddHostedService( x => x.GetRequiredService<GenerateService>());

// Twitter
var authorizer = new SingleUserAuthorizer
{
    CredentialStore = new SingleUserInMemoryCredentialStore
    {
        ConsumerKey = Environment.GetEnvironmentVariable("NIGHTMAREBOT_TWITTER_KEY"),
        ConsumerSecret = Environment.GetEnvironmentVariable("NIGHTMAREBOT_TWITTER_SECRET"),
        AccessToken = Environment.GetEnvironmentVariable("NIGHTMAREBOT_TWITTER_ACCESS_TOKEN"),
        AccessTokenSecret = Environment.GetEnvironmentVariable("NIGHTMAREBOT_TWITTER_ACCESS_TOKEN_SECRET")
    }
};
builder.Services.AddSingleton(x => new LinqToTwitter.TwitterContext(authorizer));

// Minio
builder.Services.AddSingleton(x => new Minio.MinioClient().
    WithCredentials(Environment.GetEnvironmentVariable("NIGHTMAREBOT_MINIO_KEY"), Environment.GetEnvironmentVariable("NIGHTMAREBOT_MINIO_SECRET")).
    WithEndpoint("dumb.dev")
    .Build());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();

// Discord bot

var client = app.Services.GetRequiredService<DiscordSocketClient>();
var commands = app.Services.GetRequiredService<InteractionService>();
var logger = app.Services.GetRequiredService<BotLogger>();
client.Log += logger.Log;
commands.Log += logger.Log;
await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("NIGHTMAREBOT_TOKEN"));
await client.StartAsync();
await client.SetGameAsync("the art critics", null, ActivityType.Listening);
await app.Services.GetRequiredService<CommandHandler>().InstallCommandsAsync(app.Services);
app.Run();