using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MtGArtRanker.Api.Models;

public class User
{
    public Guid Id { get; set; }
    [MaxLength(200)] public string DisplayName { get; set; } = "Default User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Ranking> Rankings { get; set; } = new();
}

public class Ranking
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Scryfall oracle_id (identifies a card across printings).</summary>
    public Guid OracleId { get; set; }

    [MaxLength(300)] public string CardName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<RankingItem> Items { get; set; } = new();
}

public class RankingItem
{
    public long Id { get; set; }
    public Guid RankingId { get; set; }
    public Ranking? Ranking { get; set; }

    /// <summary>Scryfall illustration_id (identifies a unique piece of art).</summary>
    public Guid IllustrationId { get; set; }

    /// <summary>A representative printing's Scryfall card id.</summary>
    public Guid ScryfallCardId { get; set; }

    [MaxLength(500)] public string ArtCropUrl { get; set; } = string.Empty;
    [MaxLength(500)] public string NormalImageUrl { get; set; } = string.Empty;
    [MaxLength(500)] public string ScryfallUri { get; set; } = string.Empty;
    [MaxLength(200)] public string ArtistName { get; set; } = string.Empty;
    [MaxLength(10)]  public string SetCode { get; set; } = string.Empty;

    /// <summary>1-based rank position; lower = preferred.</summary>
    public int Position { get; set; }
}

public class CardMetadataCache
{
    public Guid IllustrationId { get; set; }
    public Guid OracleId { get; set; }
    // Column type is set per-provider in AppDbContext (nvarchar(max) on SQL Server, TEXT on SQLite).
    public string Payload { get; set; } = "{}";
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
