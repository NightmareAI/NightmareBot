using Discord;
using Discord.Interactions;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareBot.Functions
{
    public class RestInteractionModule : RestInteractionModuleBase<IRestInteractionContext>
    {
        private IServiceProvider? _serviceProvider = null;
        private InteractionService _interactions;
        private readonly DiscordRestClient _discordClient;

        public RestInteractionModule(DiscordRestClient discordRestClient)
        {
            _discordClient = discordRestClient;
        }

    }
}
