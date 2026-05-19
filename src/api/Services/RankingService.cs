using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MtGArtRanker.Api.Data;
using MtGArtRanker.Api.Models;

namespace MtGArtRanker.Api.Services;

public interface IRankingService
{
    Task<IReadOnlyList<RankingDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<RankingDto?> GetAsync(Guid userId, Guid oracleId, CancellationToken ct = default);
    Task<RankingDto> UpsertAsync(Guid userId, Guid oracleId, RankingUpsertDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, Guid oracleId, CancellationToken ct = default);
}

public class RankingService : IRankingService
{
    private const int TopN = 15;
    private readonly AppDbContext _db;
    private readonly IScryfallClient _scryfall;

    public RankingService(AppDbContext db, IScryfallClient scryfall)
    {
        _db = db;
        _scryfall = scryfall;
    }

    public async Task<IReadOnlyList<RankingDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var rankings = await _db.Rankings
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Include(r => r.Items.OrderBy(i => i.Position))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

        return rankings.Select(ToDto).ToList();
    }

    public async Task<RankingDto?> GetAsync(Guid userId, Guid oracleId, CancellationToken ct = default)
    {
        var ranking = await _db.Rankings
            .AsNoTracking()
            .Include(r => r.Items.OrderBy(i => i.Position))
            .FirstOrDefaultAsync(r => r.UserId == userId && r.OracleId == oracleId, ct);

        return ranking is null ? null : ToDto(ranking);
    }

    public async Task<RankingDto> UpsertAsync(Guid userId, Guid oracleId, RankingUpsertDto dto, CancellationToken ct = default)
    {
        var ranking = await _db.Rankings
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.OracleId == oracleId, ct);

        if (ranking is null)
        {
            ranking = new Ranking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OracleId = oracleId,
                CardName = dto.CardName
            };
            _db.Rankings.Add(ranking);
        }
        else
        {
            ranking.CardName = dto.CardName;
            _db.RankingItems.RemoveRange(ranking.Items);
            ranking.Items.Clear();
        }

        // Normalize positions to 1..N based on provided ordering
        var ordered = dto.Items.OrderBy(i => i.Position).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var src = ordered[i];
            ranking.Items.Add(new RankingItem
            {
                IllustrationId = src.IllustrationId,
                ScryfallCardId = src.ScryfallCardId,
                ArtCropUrl = src.ArtCropUrl,
                NormalImageUrl = src.NormalImageUrl,
                ScryfallUri = src.ScryfallUri,
                ArtistName = src.Artist,
                SetCode = src.SetCode,
                Position = i + 1
            });
        }

        ranking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Mirror metadata for top 15 items of this ranking (per requirement Q10)
        await CacheTopMetadataAsync(ranking, ct);

        return ToDto(ranking);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid oracleId, CancellationToken ct = default)
    {
        var ranking = await _db.Rankings
            .FirstOrDefaultAsync(r => r.UserId == userId && r.OracleId == oracleId, ct);
        if (ranking is null) return false;
        _db.Rankings.Remove(ranking);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task CacheTopMetadataAsync(Ranking ranking, CancellationToken ct)
    {
        var top = ranking.Items.OrderBy(i => i.Position).Take(TopN).ToList();
        foreach (var item in top)
        {
            var existing = await _db.CardMetadataCache
                .FirstOrDefaultAsync(c => c.IllustrationId == item.IllustrationId, ct);

            var payload = JsonSerializer.Serialize(new
            {
                item.IllustrationId,
                item.ScryfallCardId,
                item.ArtCropUrl,
                item.NormalImageUrl,
                item.ScryfallUri,
                item.ArtistName,
                item.SetCode
            });

            if (existing is null)
            {
                _db.CardMetadataCache.Add(new CardMetadataCache
                {
                    IllustrationId = item.IllustrationId,
                    OracleId = ranking.OracleId,
                    Payload = payload,
                    CachedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Payload = payload;
                existing.OracleId = ranking.OracleId;
                existing.CachedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private static RankingDto ToDto(Ranking r) => new(
        r.OracleId,
        r.CardName,
        r.UpdatedAt,
        r.Items.OrderBy(i => i.Position).Select(i => new RankingItemDto(
            i.IllustrationId,
            i.ScryfallCardId,
            i.ArtCropUrl,
            i.NormalImageUrl,
            i.ScryfallUri,
            i.ArtistName,
            i.SetCode,
            i.Position)).ToList());
}