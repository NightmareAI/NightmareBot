using Discord.Rest;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(NightmareBot.Functions.Startup))]

namespace NightmareBot.Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            
            builder.Services.AddSingleton(x => new DiscordRestClient());
            builder.Services.AddSingleton<RestInteractionModule>();
        }        
    }
}
