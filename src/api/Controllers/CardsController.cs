using Microsoft.AspNetCore.Mvc;
using MtGArtRanker.Api.Models;
using MtGArtRanker.Api.Services;

namespace MtGArtRanker.Api.Controllers;

[ApiController]
[Route("api/cards")]
public class CardsController : ControllerBase
{
    private readonly IScryfallClient _scryfall;

    public CardsController(IScryfallClient scryfall) => _scryfall = scryfall;

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<string>>> Search(
        [FromQuery] string q, CancellationToken ct)
    {
        var results = await _scryfall.AutocompleteAsync(q, ct);
        return Ok(results);
    }

    [HttpGet("{idOrName}")]
    public async Task<ActionResult<CardSummaryDto>> Get(
        string idOrName, CancellationToken ct)
    {
        var card = await _scryfall.GetCardAsync(idOrName, ct);
        if (card is null || card.OracleId is null) return NotFound();
        return Ok(new CardSummaryDto(card.OracleId.Value, card.Name, card.ScryfallUri));
    }

    [HttpGet("{idOrName}/printings")]
    public async Task<ActionResult<IReadOnlyList<PrintingDto>>> GetPrintings(
        string idOrName, CancellationToken ct)
    {
        var card = await _scryfall.GetCardAsync(idOrName, ct);
        if (card is null || card.OracleId is null) return NotFound();

        var printings = await _scryfall.GetUniquePrintingsAsync(
            card.OracleId.Value.ToString(), ct);

        var result = new List<PrintingDto>();
        var seen = new HashSet<Guid>();

        foreach (var p in printings)
        {
            // Resolve illustration_id + image_uris, falling back to first face for DFCs.
            var illustrationId = p.IllustrationId
                ?? p.CardFaces?.FirstOrDefault(f => f.IllustrationId is not null)?.IllustrationId;
            var imageUris = p.ImageUris
                ?? p.CardFaces?.FirstOrDefault(f => f.ImageUris is not null)?.ImageUris;
            var artist = p.Artist
                ?? p.CardFaces?.FirstOrDefault(f => !string.IsNullOrEmpty(f.Artist))?.Artist
                ?? "Unknown";

            if (illustrationId is null || imageUris is null) continue;
            if (!seen.Add(illustrationId.Value)) continue;

            result.Add(new PrintingDto(
                illustrationId.Value,
                p.Id,
                card.OracleId.Value,
                p.Name,
                artist,
                p.Set,
                p.SetName,
                p.ReleasedAt,
                imageUris.ArtCrop ?? string.Empty,
                imageUris.Normal ?? imageUris.Large ?? string.Empty,
                p.ScryfallUri ?? string.Empty));
        }

        return Ok(result);
    }
}