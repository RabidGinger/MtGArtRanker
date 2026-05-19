namespace MtGArtRanker.Api.Models;

public record CardSummaryDto(Guid OracleId, string Name, string? ScryfallUri);

public record PrintingDto(
    Guid IllustrationId,
    Guid ScryfallCardId,
    Guid OracleId,
    string CardName,
    string Artist,
    string SetCode,
    string? SetName,
    string? ReleasedAt,
    string ArtCropUrl,
    string NormalImageUrl,
    string ScryfallUri);

public record RankingItemDto(
    Guid IllustrationId,
    Guid ScryfallCardId,
    string ArtCropUrl,
    string NormalImageUrl,
    string ScryfallUri,
    string Artist,
    string SetCode,
    int Position);

public record RankingDto(
    Guid OracleId,
    string CardName,
    DateTime UpdatedAt,
    IReadOnlyList<RankingItemDto> Items);

public record RankingUpsertDto(string CardName, IReadOnlyList<RankingItemDto> Items);