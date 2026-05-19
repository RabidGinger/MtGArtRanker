using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MtGArtRanker.Api.Services;

public interface IScryfallClient
{
    Task<IReadOnlyList<string>> AutocompleteAsync(string query, CancellationToken ct = default);
    Task<ScryfallCard?> GetCardAsync(string idOrName, CancellationToken ct = default);
    Task<IReadOnlyList<ScryfallCard>> GetUniquePrintingsAsync(string oracleId, CancellationToken ct = default);
}

public class ScryfallClient : IScryfallClient
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ScryfallClient> _log;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public ScryfallClient(HttpClient http, IMemoryCache cache, ILogger<ScryfallClient> log)
    {
        _http = http;
        _cache = cache;
        _log = log;
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUrl, CancellationToken ct)
        where T : class
    {
        using var resp = await _http.GetAsync(relativeUrl, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Scryfall {Url} returned {Status}: {Body}", relativeUrl, (int)resp.StatusCode, body);
            resp.EnsureSuccessStatusCode(); // throws — surfaces as 500 to client
        }
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
        var key = $"sf:auto:{query.ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null) return cached;

        var url = $"cards/autocomplete?q={Uri.EscapeDataString(query)}";
        var response = await GetJsonAsync<ScryfallList<string>>(url, ct);
        var data = (IReadOnlyList<string>)(response?.Data ?? new List<string>());
        _cache.Set(key, data, CacheTtl);
        return data;
    }

    public async Task<ScryfallCard?> GetCardAsync(string idOrName, CancellationToken ct = default)
    {
        var trimmed = (idOrName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        var key = $"sf:card:{trimmed.ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out ScryfallCard? cached) && cached is not null) return cached;

        ScryfallCard? card = null;

        if (Guid.TryParse(trimmed, out _))
        {
            // Try as Scryfall card id first.
            card = await GetJsonAsync<ScryfallCard>($"cards/{trimmed}", ct);

            // Then as an oracle id (one representative printing).
            if (card is null)
            {
                var page = await GetJsonAsync<ScryfallList<ScryfallCard>>(
                    $"cards/search?q=oracleid%3A{trimmed}&unique=cards&order=released&dir=asc", ct);
                card = page?.Data?.FirstOrDefault();
            }
        }

        // Fall back to named (exact, then fuzzy)
        card ??= await GetJsonAsync<ScryfallCard>(
            $"cards/named?exact={Uri.EscapeDataString(trimmed)}", ct);
        card ??= await GetJsonAsync<ScryfallCard>(
            $"cards/named?fuzzy={Uri.EscapeDataString(trimmed)}", ct);

        if (card is not null) _cache.Set(key, card, CacheTtl);
        else _log.LogInformation("Scryfall has no card matching {Input}", trimmed);
        return card;
    }

    public async Task<IReadOnlyList<ScryfallCard>> GetUniquePrintingsAsync(string oracleId, CancellationToken ct = default)
    {
        var key = $"sf:prints:{oracleId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<ScryfallCard>? cached) && cached is not null) return cached;

        // unique=art deduplicates by illustration_id server-side
        var url = $"cards/search?q=oracleid%3A{oracleId}&unique=art&order=released&dir=asc";
        var all = new List<ScryfallCard>();
        string? next = url;
        while (next is not null)
        {
            var page = await GetJsonAsync<ScryfallList<ScryfallCard>>(next, ct);
            if (page?.Data is not null) all.AddRange(page.Data);
            next = page?.HasMore == true && page.NextPage is not null
                ? page.NextPage.Replace("https://api.scryfall.com/", "", StringComparison.OrdinalIgnoreCase)
                : null;
        }

        _cache.Set(key, all, CacheTtl);
        return all;
    }
}

public class ScryfallList<T>
{
    [JsonPropertyName("data")] public List<T>? Data { get; set; }
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
    [JsonPropertyName("next_page")] public string? NextPage { get; set; }
}

public class ScryfallCard
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("oracle_id")] public Guid? OracleId { get; set; }
    [JsonPropertyName("illustration_id")] public Guid? IllustrationId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("artist")] public string? Artist { get; set; }
    [JsonPropertyName("set")] public string Set { get; set; } = string.Empty;
    [JsonPropertyName("set_name")] public string? SetName { get; set; }
    [JsonPropertyName("released_at")] public string? ReleasedAt { get; set; }
    [JsonPropertyName("scryfall_uri")] public string? ScryfallUri { get; set; }
    [JsonPropertyName("image_uris")] public ScryfallImageUris? ImageUris { get; set; }
    [JsonPropertyName("card_faces")] public List<ScryfallCardFace>? CardFaces { get; set; }
}

public class ScryfallCardFace
{
    [JsonPropertyName("illustration_id")] public Guid? IllustrationId { get; set; }
    [JsonPropertyName("artist")] public string? Artist { get; set; }
    [JsonPropertyName("image_uris")] public ScryfallImageUris? ImageUris { get; set; }
}

public class ScryfallImageUris
{
    [JsonPropertyName("art_crop")] public string? ArtCrop { get; set; }
    [JsonPropertyName("normal")] public string? Normal { get; set; }
    [JsonPropertyName("large")] public string? Large { get; set; }
}