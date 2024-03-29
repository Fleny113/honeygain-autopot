using System.Net;
using System.Net.Http.Json;
using Discord;
using Discord.Webhook;
using Microsoft.Extensions.Options;

namespace HoneyGainAutoPot;

public sealed partial class RewardPotClaimer(ILogger<RewardPotClaimer> logger, IOptions<HoneyGainApplicationSettings> settings, IHttpClientFactory httpClientFactory)
    : BackgroundService
{
    private DiscordWebhookClient? _webhook;
    private static DateTimeOffset _lastRun = DateTimeOffset.UtcNow.AddDays(-1);
    private static bool _isFirstRun = true;

    private const string AvatarUrl = "https://s3-eu-west-1.amazonaws.com/tpd/logos/5db47bcc4de43a0001b54999/0x0.png";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested && (_isFirstRun || await periodicTimer.WaitForNextTickAsync(stoppingToken)))
        {
            try
            {
                _webhook ??= new DiscordWebhookClient(settings.Value.WebhookUrl);
                await CheckAndClaimPot(_webhook, stoppingToken);
            }
            catch (HttpRequestException e)
            {
                await Console.Error.WriteLineAsync($"Could not send an HTTP request to check/claim the pot or send the message to discord. {e}");
            }
        }
    }

    private async Task CheckAndClaimPot(DiscordWebhookClient webhook, CancellationToken stoppingToken)
    {
        _isFirstRun = false;

        if (_lastRun.Day == DateTimeOffset.UtcNow.Day)
            return;

        var httpClient = httpClientFactory.CreateClient("HoneyGain");

        // Try get the current status of the lucky pot
        var getWinningsRequest = await httpClient.GetAsync("contest_winnings", stoppingToken);

        if (!getWinningsRequest.IsSuccessStatusCode)
        {
            LogFailedCurrentPotFetch(logger, DateTimeOffset.Now, getWinningsRequest.StatusCode);
            return;
        }

        var getWinningsResponse =
            await getWinningsRequest.Content.ReadFromJsonAsync<HoneyGainResponse<GetWinningsData>>(cancellationToken: stoppingToken);

        // We want to ignore the request and try later
        if (getWinningsResponse is null)
            return;

        LogSuccessCurrentPotFetch(logger, DateTimeOffset.Now, getWinningsRequest.StatusCode, getWinningsResponse);

        // Check if the current progress is less then the needed amount, if so, retry later
        if (getWinningsResponse.Data.ProgressBytes < getWinningsResponse.Data.MaxBytes)
            return;

        // The pot is already claimed, mark the day as done and check again later
        if (getWinningsResponse.Data.WinningCredits is not null)
        {
            _lastRun = DateTimeOffset.UtcNow;
            return;
        }

        // Redeem the pot
        var claimWinningsRequest = await httpClient.PostAsync("contest_winnings", null, stoppingToken);

        if (!claimWinningsRequest.IsSuccessStatusCode)
        {
            LogFailedRedeemPotFetch(logger, DateTimeOffset.Now, claimWinningsRequest.StatusCode);
            return;
        }

        var claimWinningsResponse =
            await claimWinningsRequest.Content.ReadFromJsonAsync<HoneyGainResponse<ClaimWinningsData>>(cancellationToken: stoppingToken);

        // There was an error reading the request body, retry later (as this is a redeem, we might miss the WebHook call)
        if (claimWinningsResponse is null)
            return;

        LogSuccessRedeemPotFetch(logger, DateTimeOffset.Now, claimWinningsRequest.StatusCode, claimWinningsResponse);
        _lastRun = DateTimeOffset.UtcNow;

        await SendDiscordWebhookMessageAsync(webhook, claimWinningsResponse.Data);
    }

    private static async Task SendDiscordWebhookMessageAsync(DiscordWebhookClient webhook, ClaimWinningsData data)
    {
        var embed = new EmbedBuilder()
           .WithTitle("HoneyGain lucky pot")
           .WithDescription($"You got {data.Credits} credits!")
           .WithColor(Color.Green)
           .WithTimestamp(DateTimeOffset.Now)
           .Build();

        await webhook.SendMessageAsync(embeds: new[] { embed }, username: "HoneyGain lucky pot auto-claimer", avatarUrl: AvatarUrl);
    }

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "{DateTime} | Failed to get the current lucky pot status, statusCode: {StatusCode}")]
    private static partial void LogFailedCurrentPotFetch(ILogger logger, DateTimeOffset dateTime, HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "{DateTime} | Failed to claim the lucky pot, statusCode: {StatusCode}")]
    private static partial void LogFailedRedeemPotFetch(ILogger logger, DateTimeOffset dateTime, HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Debug,
        Message = "{DateTime} | Executed request GET /contest_winnings | Status Code: {StatusCode} | Response: {Response}")]
    private static partial void LogSuccessCurrentPotFetch(ILogger logger, DateTimeOffset dateTime, HttpStatusCode statusCode,
        HoneyGainResponse<GetWinningsData> response);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Debug,
        Message = "{DateTime} | Executed request POST /contest_winnings | Status Code: {StatusCode} | Response: {Response}")]
    private static partial void LogSuccessRedeemPotFetch(ILogger logger, DateTimeOffset dateTime, HttpStatusCode statusCode,
        HoneyGainResponse<ClaimWinningsData> response);
}
