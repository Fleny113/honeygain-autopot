using System.Text.Json.Serialization;

namespace HoneyGainAutoRewardPot;

internal record HoneyGainResponse<TData>(
    [property: JsonPropertyName("meta")] object? Meta,
    [property: JsonPropertyName("data")] TData Data
);

internal record GetWinningsData(
    [property: JsonPropertyName("progress_bytes")] int ProgressBytes,
    [property: JsonPropertyName("max_bytes")] int MaxBytes,
    [property: JsonPropertyName("winning_credits")] double? WinningCredits
);

internal record ClaimWinningsData(
    [property: JsonPropertyName("credits")] double Credits
);
