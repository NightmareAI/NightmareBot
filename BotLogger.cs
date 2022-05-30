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
        switch (msg.Severity)
        {
            case LogSeverity.Debug:
                if (msg.Exception != null)
                    _logger.LogDebug(msg.Exception, msg.Message);
                else
                    _logger.LogDebug(msg.Message);
                break;
            case LogSeverity.Verbose:
                if (msg.Exception != null)
                    _logger.LogDebug(msg.Exception, msg.Message);
                else
                    _logger.LogDebug(msg.Message);
                break;
            case LogSeverity.Info:
            default:
                if (msg.Exception != null)
                    _logger.LogInformation(msg.Exception, msg.Message);
                else
                    _logger.LogInformation(msg.Message);
                break;
            case LogSeverity.Warning:
                if (msg.Exception != null)
                    _logger.LogWarning(msg.Exception, msg.Message);
                else
                    _logger.LogWarning(msg.Message);
                break;
            case LogSeverity.Error:
                if (msg.Exception != null)
                    _logger.LogError(msg.Exception, msg.Message);
                else
                    _logger.LogError(msg.Message);
                break;
            case LogSeverity.Critical:
                if (msg.Exception != null)
                    _logger.LogCritical(msg.Exception, msg.Message);
                else
                    _logger.LogCritical(msg.Message);
                break;
            
        }
        _logger.LogInformation(msg.Message);
        return Task.CompletedTask;
    }
}