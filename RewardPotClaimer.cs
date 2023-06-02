using System.Net.Http.Json;
using System.Text.Json;
using Discord;
using Discord.Webhook;
using Microsoft.Extensions.Options;

namespace HoneyGainAutoRewardPot;

public sealed class RewardPotClaimer : BackgroundService
{
    private readonly ILogger<RewardPotClaimer> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _interval;
    private readonly DiscordWebhookClient _webhook;

    private static DateTimeOffset _lastRun = DateTimeOffset.UtcNow.AddDays(-1);

    public RewardPotClaimer(ILogger<RewardPotClaimer> logger, IOptions<HoneyGainApplicationSettings> settings, IHttpClientFactory httpClientFactory, IHostEnvironment environment)
    {
        _logger = logger;

        _httpClient = httpClientFactory.CreateClient("HoneyGain");

        _webhook = new DiscordWebhookClient(settings.Value.WebhookUrl);

        _interval = environment.IsDevelopment()
            ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromHours(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var periodicTimer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            if (_lastRun.Day == DateTimeOffset.UtcNow.Day)
                continue;

            var getWinningsRequest = await _httpClient.GetAsync("contest_winnings", stoppingToken);

            if (!getWinningsRequest.IsSuccessStatusCode)
            {
                _logger.LogError("{DateTime} | Failed to get the current lucky pot status, statusCode: {StatusCode}",
                    DateTimeOffset.Now,
                    getWinningsRequest.StatusCode);

                continue;
            }

            var getWinningsResponse = await getWinningsRequest.Content.ReadFromJsonAsync<HoneyGainResponse<GetWinningsData>>(cancellationToken: stoppingToken);

            if (getWinningsResponse is null)
                throw new NullReferenceException("Cannot parse body!");

            _logger.LogDebug("{DateTime} | Executed request GET contest_winnings | Status Code: {StatusCode} | Response: {Response}",
                DateTimeOffset.Now,
                getWinningsRequest.StatusCode,
                JsonSerializer.Serialize(getWinningsResponse));

            if (getWinningsResponse.Data.ProgressBytes < getWinningsResponse.Data.MaxBytes)
                continue;

            if (getWinningsResponse.Data.WinningCredits is not null)
            {
                _lastRun = DateTimeOffset.UtcNow;
                continue;
            }

            var claimWinningsRequest = await _httpClient.PostAsync("contest_winnings", null, stoppingToken);

            if (!claimWinningsRequest.IsSuccessStatusCode)
            {
                _logger.LogError("{DateTime} | Failed to claim the lucky pot, statusCode: {StatusCode}",
                    DateTimeOffset.Now,
                    claimWinningsRequest.StatusCode);

                continue;
            }

            var claimWinningsResponse = await claimWinningsRequest.Content.ReadFromJsonAsync<HoneyGainResponse<ClaimWinningsData>>(cancellationToken: stoppingToken);

            if (claimWinningsResponse is null)
                throw new NullReferenceException("Cannot parse body!");

            _logger.LogDebug("{DateTime} | Executed request POST contest_winnings | Status Code: {StatusCode} | Response: {Response}",
                DateTimeOffset.Now,
                claimWinningsRequest.StatusCode,
                JsonSerializer.Serialize(claimWinningsResponse));

            await SendDiscordWebhookMessageAsync(claimWinningsResponse.Data);

            _lastRun = DateTimeOffset.UtcNow;
        }
    }

    private const string AvatarUrl = "https://s3-eu-west-1.amazonaws.com/tpd/logos/5db47bcc4de43a0001b54999/0x0.png";

    private async Task SendDiscordWebhookMessageAsync(ClaimWinningsData data)
    {
        var embed = new EmbedBuilder()
           .WithTitle("HoneyGain lucky pot")
           .WithDescription($"You got {data.Credits} credits!")
           .WithColor(Color.Green)
           .WithTimestamp(DateTimeOffset.Now)
           .Build();

        await _webhook.SendMessageAsync(embeds: new[] { embed }, username: "HoneyGain lucky pot auto-claimer", avatarUrl: AvatarUrl);
    }
}
