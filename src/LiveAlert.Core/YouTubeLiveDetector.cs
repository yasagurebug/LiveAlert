using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LiveAlert.Core;

public interface ILiveDetector
{
    Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken cancellationToken);
}

public sealed class YouTubeLiveDetector : ILiveDetector
{
    private readonly HttpClient _httpClient;
    private static readonly Regex IsLiveNowRegex = new("\"isLiveNow\":(true|false)", RegexOptions.Compiled);
    private static readonly Regex InitialDataRegex = new(@"var ytInitialData\s*=\s*(\{.*?\});", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex VideoIdInStringRegex = new(@"(?:/vi/|[?&]v=)([A-Za-z0-9_-]{11})", RegexOptions.Compiled);

    public YouTubeLiveDetector(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
        }
    }

    public async Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken cancellationToken)
    {
        try
        {
            var url = alert.Url.Trim();
            if (url.Contains("watch?v=", StringComparison.OrdinalIgnoreCase))
            {
                var videoId = ExtractVideoIdFromUrl(url);
                if (string.IsNullOrEmpty(videoId))
                {
                    return LiveCheckResult.NotLive();
                }

                return await CheckWatchPageAsync(videoId, cancellationToken).ConfigureAwait(false);
            }

            var baseUrl = NormalizeChannelUrl(url);
            var streamsUrl = $"{baseUrl}/streams";
            var (streamsOk, streamsHtml, streamsError) = await FetchStringAsync(streamsUrl, cancellationToken).ConfigureAwait(false);
            if (!streamsOk)
            {
                return streamsError ?? LiveCheckResult.Error("HTTP error");
            }

            var candidateId = ExtractLiveVideoId(streamsHtml);
            if (string.IsNullOrEmpty(candidateId))
            {
                var (homeOk, homeHtml, homeError) = await FetchStringAsync(baseUrl, cancellationToken).ConfigureAwait(false);
                if (!homeOk)
                {
                    return homeError ?? LiveCheckResult.Error("HTTP error");
                }
                candidateId = ExtractLiveVideoId(homeHtml);
            }

            if (string.IsNullOrEmpty(candidateId))
            {
                return LiveCheckResult.NotLive();
            }

            return await CheckWatchPageAsync(candidateId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            return LiveCheckResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return LiveCheckResult.Error(ex.Message);
        }
    }

    private async Task<LiveCheckResult> CheckWatchPageAsync(string videoId, CancellationToken cancellationToken)
    {
        var watchUrl = $"https://www.youtube.com/watch?v={videoId}";
        var (watchOk, html, watchError) = await FetchStringAsync(watchUrl, cancellationToken).ConfigureAwait(false);
        if (!watchOk)
        {
            return watchError ?? LiveCheckResult.Error("HTTP error");
        }
        var liveMatch = IsLiveNowRegex.Match(html ?? string.Empty);
        if (liveMatch.Success && liveMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return LiveCheckResult.Live(videoId);
        }

        return LiveCheckResult.NotLive();
    }

    private static string NormalizeChannelUrl(string url)
    {
        url = url.TrimEnd('/');
        var lowered = url.ToLowerInvariant();
        foreach (var suffix in new[] { "/streams", "/live", "/videos", "/featured" })
        {
            if (lowered.EndsWith(suffix, StringComparison.Ordinal))
            {
                return url[..^suffix.Length];
            }
        }
        return url;
    }

    private static string? ExtractLiveVideoId(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        var match = InitialDataRegex.Match(html);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(match.Groups[1].Value);
            return FindLiveVideoId(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractVideoIdFromUrl(string url)
    {
        var idx = url.IndexOf("watch?v=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var part = url[(idx + "watch?v=".Length)..];
        var amp = part.IndexOf('&');
        if (amp >= 0)
        {
            part = part[..amp];
        }
        return part.Length == 11 ? part : null;
    }

    private async Task<(bool ok, string? content, LiveCheckResult? error)> FetchStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, LiveCheckResult.Error($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"));
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (true, html, null);
    }

    private static string? FindLiveVideoId(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (element.TryGetProperty("videoId", out var videoIdElement) &&
                    videoIdElement.ValueKind == JsonValueKind.String &&
                    element.TryGetProperty("thumbnailOverlays", out var overlaysElement) &&
                    overlaysElement.ValueKind == JsonValueKind.Array)
                {
                    if (HasLiveOverlay(overlaysElement))
                    {
                        return videoIdElement.GetString();
                    }
                }

                if (IsLikelyVideoItem(element) &&
                    HasLiveBadge(element) &&
                    TryFindVideoIdReference(element, out var liveItemVideoId))
                {
                    return liveItemVideoId;
                }

                foreach (var property in element.EnumerateObject())
                {
                    var found = FindLiveVideoId(property.Value);
                    if (!string.IsNullOrEmpty(found))
                    {
                        return found;
                    }
                }

                break;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindLiveVideoId(item);
                    if (!string.IsNullOrEmpty(found))
                    {
                        return found;
                    }
                }

                break;
            }
        }

        return null;
    }

    private static bool IsLikelyVideoItem(JsonElement element)
    {
        return element.TryGetProperty("richItemRenderer", out _) ||
               element.TryGetProperty("lockupViewModel", out _) ||
               element.TryGetProperty("videoRenderer", out _) ||
               element.TryGetProperty("gridVideoRenderer", out _) ||
               element.TryGetProperty("compactVideoRenderer", out _);
    }

    private static bool HasLiveBadge(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (element.TryGetProperty("thumbnailBadgeViewModel", out var badge) &&
                    badge.ValueKind == JsonValueKind.Object &&
                    IsLiveThumbnailBadge(badge))
                {
                    return true;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (HasLiveBadge(property.Value))
                    {
                        return true;
                    }
                }

                break;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (HasLiveBadge(item))
                    {
                        return true;
                    }
                }

                break;
            }
        }

        return false;
    }

    private static bool IsLiveThumbnailBadge(JsonElement badge)
    {
        if (badge.TryGetProperty("badgeStyle", out var badgeStyle) &&
            badgeStyle.ValueKind == JsonValueKind.String &&
            badgeStyle.GetString()?.Equals("THUMBNAIL_OVERLAY_BADGE_STYLE_LIVE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (badge.TryGetProperty("text", out var text) &&
            text.ValueKind == JsonValueKind.String &&
            text.GetString()?.Contains("ライブ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return false;
    }

    private static bool TryFindVideoIdReference(JsonElement element, out string videoId)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        TryExtractVideoIdFromString(property.Value.GetString(), out videoId))
                    {
                        return true;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindVideoIdReference(property.Value, out videoId))
                    {
                        return true;
                    }
                }

                break;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindVideoIdReference(item, out videoId))
                    {
                        return true;
                    }
                }

                break;
            }
            case JsonValueKind.String:
                if (TryExtractVideoIdFromString(element.GetString(), out videoId))
                {
                    return true;
                }
                break;
        }

        videoId = string.Empty;
        return false;
    }

    private static bool TryExtractVideoIdFromString(string? value, out string videoId)
    {
        if (!string.IsNullOrEmpty(value))
        {
            if (value.Length == 11 && value.All(IsVideoIdChar))
            {
                videoId = value;
                return true;
            }

            var match = VideoIdInStringRegex.Match(value);
            if (match.Success)
            {
                videoId = match.Groups[1].Value;
                return true;
            }
        }

        videoId = string.Empty;
        return false;
    }

    private static bool IsVideoIdChar(char value)
    {
        return value is >= 'A' and <= 'Z' ||
               value is >= 'a' and <= 'z' ||
               value is >= '0' and <= '9' ||
               value == '_' ||
               value == '-';
    }

    private static bool HasLiveOverlay(JsonElement overlaysElement)
    {
        foreach (var overlay in overlaysElement.EnumerateArray())
        {
            if (!overlay.TryGetProperty("thumbnailOverlayTimeStatusRenderer", out var renderer) ||
                renderer.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (renderer.TryGetProperty("style", out var styleElement) &&
                styleElement.ValueKind == JsonValueKind.String &&
                styleElement.GetString()?.Equals("LIVE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (renderer.TryGetProperty("text", out var textElement) &&
                TextContainsLiveLabel(textElement))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TextContainsLiveLabel(JsonElement textElement)
    {
        if (textElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (textElement.TryGetProperty("simpleText", out var simpleText) &&
            simpleText.ValueKind == JsonValueKind.String &&
            simpleText.GetString()?.Contains("ライブ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (textElement.TryGetProperty("runs", out var runsElement) &&
            runsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var run in runsElement.EnumerateArray())
            {
                if (run.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (run.TryGetProperty("text", out var runText) &&
                    runText.ValueKind == JsonValueKind.String &&
                    runText.GetString()?.Contains("ライブ", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public readonly record struct LiveCheckResult(bool IsLive, string? VideoId, bool IsError, string? ErrorMessage)
{
    public static LiveCheckResult Live(string videoId) => new(true, videoId, false, null);
    public static LiveCheckResult NotLive() => new(false, null, false, null);
    public static LiveCheckResult Error(string message) => new(false, null, true, message);
}
