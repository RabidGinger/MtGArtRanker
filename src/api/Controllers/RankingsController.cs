using Microsoft.AspNetCore.Mvc;
using MtGArtRanker.Api.Data;
using MtGArtRanker.Api.Models;
using MtGArtRanker.Api.Services;

namespace MtGArtRanker.Api.Controllers;

[ApiController]
[Route("api/rankings")]
public class RankingsController : ControllerBase
{
    private readonly IRankingService _rankings;

    public RankingsController(IRankingService rankings) => _rankings = rankings;

    // MVP: single-user. Replace with auth-derived UserId in v2.
    private static Guid CurrentUserId => AppDbContext.DefaultUserId;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RankingDto>>> List(CancellationToken ct)
        => Ok(await _rankings.ListAsync(CurrentUserId, ct));

    [HttpGet("{oracleId:guid}")]
    public async Task<ActionResult<RankingDto>> Get(Guid oracleId, CancellationToken ct)
    {
        var ranking = await _rankings.GetAsync(CurrentUserId, oracleId, ct);
        return ranking is null ? NotFound() : Ok(ranking);
    }

    [HttpPut("{oracleId:guid}")]
    public async Task<ActionResult<RankingDto>> Upsert(
        Guid oracleId, [FromBody] RankingUpsertDto body, CancellationToken ct)
    {
        if (body is null) return BadRequest();
        var result = await _rankings.UpsertAsync(CurrentUserId, oracleId, body, ct);
        return Ok(result);
    }

    [HttpDelete("{oracleId:guid}")]
    public async Task<IActionResult> Delete(Guid oracleId, CancellationToken ct)
    {
        var deleted = await _rankings.DeleteAsync(CurrentUserId, oracleId, ct);
        return deleted ? NoContent() : NotFound();
    }
}