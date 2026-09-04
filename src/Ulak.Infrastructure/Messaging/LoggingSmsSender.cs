using Ulak.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ulak.Infrastructure.Messaging;

/// <summary>
/// Development / not-yet-integrated SMS sender: writes the message to the log
/// instead of hitting a provider. Swap for a real <see cref="ISmsSender"/>
/// (Netgsm, Twilio, ...) by registering it after this one in DI.
/// </summary>
public sealed class LoggingSmsSender : ISmsSender
{
    private readonly SmsOptions _options;
    private readonly ILogger<LoggingSmsSender> _logger;

    public LoggingSmsSender(IOptions<SmsOptions> options, ILogger<LoggingSmsSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendAsync(string toPhone, string body, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SMS disabled; would have sent to {Phone}: {Body}", toPhone, body);
            return Task.CompletedTask;
        }

        _logger.LogInformation("SMS -> {Phone}: {Body}", toPhone, body);
        return Task.CompletedTask;
    }
}
