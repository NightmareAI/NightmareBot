using Discord;

namespace NightmareBot;

public class BotLogger
{
    private readonly ILogger<BotLogger> _logger;

    public BotLogger(ILogger<BotLogger> logger)
    {
        _logger = logger;
    }
    
    public Task Log(LogMessage msg)
    {
        _logger.LogInformation(msg.Message);
        return Task.CompletedTask;
    }
}