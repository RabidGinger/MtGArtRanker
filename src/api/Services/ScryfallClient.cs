using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

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
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public ScryfallClient(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
        var key = $"sf:auto:{query.ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null) return cached;

        var url = $"cards/autocomplete?q={Uri.EscapeDataString(query)}";
        var response = await _http.GetFromJsonAsync<ScryfallList<string>>(url, ct);
        var data = (IReadOnlyList<string>)(response?.Data ?? new List<string>());
        _cache.Set(key, data, CacheTtl);
        return data;
    }

    public async Task<ScryfallCard?> GetCardAsync(string idOrName, CancellationToken ct = default)
    {
        var key = $"sf:card:{idOrName.ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out ScryfallCard? cached) && cached is not null) return cached;

        // Try as Scryfall id (UUID) first
        ScryfallCard? card = null;
        if (Guid.TryParse(idOrName, out _))
        {
            try { card = await _http.GetFromJsonAsync<ScryfallCard>($"cards/{idOrName}", ct); }
            catch (HttpRequestException) { card = null; }
        }

        if (card is null)
        {
            // Fall back to named (exact, then fuzzy)
            try { card = await _http.GetFromJsonAsync<ScryfallCard>($"cards/named?exact={Uri.EscapeDataString(idOrName)}", ct); }
            catch (HttpRequestException)
            {
                try { card = await _http.GetFromJsonAsync<ScryfallCard>($"cards/named?fuzzy={Uri.EscapeDataString(idOrName)}", ct); }
                catch (HttpRequestException) { card = null; }
            }
        }

        if (card is not null) _cache.Set(key, card, CacheTtl);
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
            var page = await _http.GetFromJsonAsync<ScryfallList<ScryfallCard>>(next, ct);
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