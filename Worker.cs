using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoneyGainAutoRewardPot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _interval;

    private static DateTime _lastRun = DateTime.UtcNow.AddDays(-1);

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("HoneyGain");

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

            var responseBody = await contestWinningGetRequest.Content.ReadFromJsonAsync<ContestWinningsPayload>(cancellationToken: stoppingToken);

            if (responseBody is null) throw new NullReferenceException("Cannot parse null body!");
            
            _logger.LogDebug("{DateTime} | Executed request GET contest_winnings | Status Code: {StatusCode} | Response: {Response}",
                DateTime.UtcNow,
                contestWinningGetRequest.StatusCode, 
                JsonSerializer.Serialize(responseBody));

            if (responseBody.Data.ProgressBytes < responseBody.Data.MaxBytes) 
                continue;

            if (responseBody.Data.WinningCredits is not null)
            {
                _lastRun = DateTime.UtcNow;
                continue;
            }

            var contestWinningPostRequest = await _httpClient.PostAsync("contest_winnings", null, stoppingToken);

            if (contestWinningPostRequest.StatusCode != HttpStatusCode.OK) 
                throw new Exception("Error trying to claim the thing");
            
            _lastRun = DateTime.UtcNow;
        }
    }
    
    // ReSharper disable once ClassNeverInstantiated.Local
    private record ContestWinningsPayload(
        // ReSharper disable once NotAccessedPositionalProperty.Local
        [property: JsonPropertyName("meta")] object? Meta, 
        [property: JsonPropertyName("data")] InnerContestWinningsPayload Data);
    
    // ReSharper disable once ClassNeverInstantiated.Local
    private record InnerContestWinningsPayload(
        [property: JsonPropertyName("progress_bytes")] int ProgressBytes, 
        [property: JsonPropertyName("max_bytes")] int MaxBytes,
        [property: JsonPropertyName("winning_credits")] double? WinningCredits);
}
