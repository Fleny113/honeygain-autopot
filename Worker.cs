using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.Webhook;
using Microsoft.Extensions.Options;

namespace HoneyGainAutoRewardPot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _interval;
    private readonly DiscordWebhookClient _webhook;

    private static DateTime _lastRun = DateTime.UtcNow.AddDays(-1);

    public Worker(ILogger<Worker> logger, IOptions<HoneyGainApplicationSettings> settings, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("HoneyGain");

        _webhook = new DiscordWebhookClient(settings.Value.WebhookUrl);
        
#if !DEBUG
        _interval = TimeSpan.FromHours(1);
#else
        _interval = TimeSpan.FromSeconds(30);
#endif
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var periodicTimer = new PeriodicTimer(_interval);
        
        while (!stoppingToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            if (_lastRun.Day == DateTime.UtcNow.Day) 
                continue;
            
            var contestWinningGetRequest = await _httpClient.GetAsync("contest_winnings", stoppingToken);

            if (!contestWinningGetRequest.IsSuccessStatusCode)
            {
                _logger.LogError("{DateTime} | Failed to get the current lucky pot status, statusCode: {StatusCode}",
                    DateTime.Now,
                    contestWinningGetRequest.StatusCode);

                continue;
            }

            var getPayload = await contestWinningGetRequest.Content.ReadFromJsonAsync<GetContestWinningsPayload>(cancellationToken: stoppingToken);

            if (getPayload is null) 
                throw new NullReferenceException("Cannot parse body!");
            
            _logger.LogDebug("{DateTime} | Executed request GET contest_winnings | Status Code: {StatusCode} | Response: {Response}",
                DateTime.Now,
                contestWinningGetRequest.StatusCode, 
                JsonSerializer.Serialize(getPayload));

            if (getPayload.Data.ProgressBytes < getPayload.Data.MaxBytes) 
                continue;

            if (getPayload.Data.WinningCredits is not null)
            {
                _lastRun = DateTime.UtcNow;
                continue;
            }

            var contestWinningsClaimRequest = await _httpClient.PostAsync("contest_winnings", null, stoppingToken);

            if (!contestWinningsClaimRequest.IsSuccessStatusCode)
            {
                _logger.LogError("{DateTime} | Failed to claim the lucky pot, statusCode: {StatusCode}",
                    DateTime.Now,
                    contestWinningsClaimRequest.StatusCode);

                continue;
            }

            var claimPayload = await contestWinningsClaimRequest.Content.ReadFromJsonAsync<ClaimContestWinningsPayload>(cancellationToken: stoppingToken);
            
            if (claimPayload is null) 
                throw new NullReferenceException("Cannot parse body!");
            
            _logger.LogDebug("{DateTime} | Executed request POST contest_winnings | Status Code: {StatusCode} | Response: {Response}",
                DateTime.Now,
                contestWinningsClaimRequest.StatusCode, 
                JsonSerializer.Serialize(claimPayload));

            await SendDiscordWebhookMessageAsync(claimPayload.Data);
            
            _lastRun = DateTime.UtcNow;
        }
    }

    private const string AvatarUrl = "https://th.bing.com/th/id/OIP.yPdcsofpZ5jxhQMu72zK-wHaHa?pid=ImgDet&rs=1";

    private async Task SendDiscordWebhookMessageAsync(InnerClaimContestWinningsPayload payload)
    {
        var embed = new EmbedBuilder()
           .WithTitle("HoneyGain lucky pot")
           .WithDescription($"You got {payload.WinningCredits} credits!")
           .WithColor(Color.Green)
           .WithTimestamp(DateTimeOffset.Now)
           .Build();

        await _webhook.SendMessageAsync(embeds: new[] { embed }, username: "HoneyGain lucky pot auto-claimer", avatarUrl: AvatarUrl);
    }
    
    // ReSharper disable once ClassNeverInstantiated.Local
    private record GetContestWinningsPayload(
        // ReSharper disable once NotAccessedPositionalProperty.Local
        [property: JsonPropertyName("meta")] 
        object? Meta,
        [property: JsonPropertyName("data")] 
        InnerGetContestWinningsPayload Data
    );

    // ReSharper disable once ClassNeverInstantiated.Local
    private record InnerGetContestWinningsPayload(
        [property: JsonPropertyName("progress_bytes")]
        int ProgressBytes,
        [property: JsonPropertyName("max_bytes")]
        int MaxBytes,
        [property: JsonPropertyName("winning_credits")]
        double? WinningCredits
    );
    
    // ReSharper disable once ClassNeverInstantiated.Local
    private record ClaimContestWinningsPayload(
        // ReSharper disable once NotAccessedPositionalProperty.Local
        [property: JsonPropertyName("meta")] 
        object? Meta,
        [property: JsonPropertyName("data")] 
        InnerClaimContestWinningsPayload Data
    );

    // ReSharper disable once ClassNeverInstantiated.Local
    private record InnerClaimContestWinningsPayload(
        [property: JsonPropertyName("credits")]
        double WinningCredits
    );
}
