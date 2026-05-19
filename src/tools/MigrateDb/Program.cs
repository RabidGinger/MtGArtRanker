using Microsoft.EntityFrameworkCore;
using MtGArtRanker.Api.Data;
using MtGArtRanker.Api.Models;

// Copies all data from a SQLite file (the local dev DB) into a SQL Server
// database (Azure SQL or LocalDB). The destination schema is created via
// EF Core migrations.
//
// Usage:
//   dotnet run --project src/tools/MigrateDb -- \
//       --from "Data Source=src/api/mtgartranker.db" \
//       --to   "Server=tcp:my-sql.database.windows.net,1433;Initial Catalog=mtgartranker;Authentication=Active Directory Default;Encrypt=True;"

var fromConn = GetArg(args, "--from") ?? "Data Source=mtgartranker.db";
var toConn = GetArg(args, "--to")
    ?? throw new ArgumentException("--to <connection-string> is required (target SQL Server).");
var force = args.Contains("--force");

Console.WriteLine($"From (SQLite):     {Redact(fromConn)}");
Console.WriteLine($"To   (SQL Server): {Redact(toConn)}");
Console.WriteLine();

// Source
var srcOpts = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite(fromConn)
    .Options;
using var src = new AppDbContext(srcOpts);

if (!await src.Database.CanConnectAsync())
{
    Console.Error.WriteLine("ERROR: cannot open the source SQLite database.");
    return 2;
}

// Destination
var dstOpts = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(toConn)
    .Options;
using var dst = new AppDbContext(dstOpts);

Console.WriteLine("Applying migrations on destination…");
await dst.Database.MigrateAsync();

// Refuse to overwrite an existing dataset unless --force is given.
var anyRanking = await dst.Rankings.AnyAsync();
if (anyRanking && !force)
{
    Console.Error.WriteLine("ERROR: destination already contains rankings. Re-run with --force to merge.");
    return 3;
}

// Pull source data into memory (single user, so this is small)
var users = await src.Users.AsNoTracking().ToListAsync();
var rankings = await src.Rankings.AsNoTracking().ToListAsync();
var items = await src.RankingItems.AsNoTracking().ToListAsync();
var cache = await src.CardMetadataCache.AsNoTracking().ToListAsync();

Console.WriteLine($"Source: {users.Count} users, {rankings.Count} rankings, {items.Count} items, {cache.Count} cache rows.");

// Upsert users (the default user row is seeded by migrations, so skip duplicates)
var dstUserIds = (await dst.Users.Select(u => u.Id).ToListAsync()).ToHashSet();
foreach (var u in users)
    if (!dstUserIds.Contains(u.Id)) dst.Users.Add(u);
await dst.SaveChangesAsync();

// Upsert rankings (idempotent by (UserId, OracleId))
var existing = await dst.Rankings
    .Include(r => r.Items)
    .ToDictionaryAsync(r => (r.UserId, r.OracleId));

foreach (var r in rankings)
{
    if (existing.TryGetValue((r.UserId, r.OracleId), out var found))
    {
        // Replace items.
        dst.RankingItems.RemoveRange(found.Items);
        found.CardName = r.CardName;
        found.UpdatedAt = r.UpdatedAt;
        await dst.SaveChangesAsync();

        foreach (var it in items.Where(i => i.RankingId == r.Id))
        {
            dst.RankingItems.Add(new RankingItem
            {
                // Id auto-generated (long identity in SQL Server)
                RankingId = found.Id,
                IllustrationId = it.IllustrationId,
                ScryfallCardId = it.ScryfallCardId,
                ArtCropUrl = it.ArtCropUrl,
                NormalImageUrl = it.NormalImageUrl,
                ScryfallUri = it.ScryfallUri,
                ArtistName = it.ArtistName,
                SetCode = it.SetCode,
                Position = it.Position,
            });
        }
    }
    else
    {
        dst.Rankings.Add(new Ranking
        {
            Id = r.Id,
            UserId = r.UserId,
            OracleId = r.OracleId,
            CardName = r.CardName,
            UpdatedAt = r.UpdatedAt,
            Items = items.Where(i => i.RankingId == r.Id)
                .Select(i => new RankingItem
                {
                    // Id auto-generated
                    RankingId = r.Id,
                    IllustrationId = i.IllustrationId,
                    ScryfallCardId = i.ScryfallCardId,
                    ArtCropUrl = i.ArtCropUrl,
                    NormalImageUrl = i.NormalImageUrl,
                    ScryfallUri = i.ScryfallUri,
                    ArtistName = i.ArtistName,
                    SetCode = i.SetCode,
                    Position = i.Position,
                })
                .ToList(),
        });
    }
}

// Upsert cache entries (key: IllustrationId)
var dstCacheIds = (await dst.CardMetadataCache.Select(c => c.IllustrationId).ToListAsync()).ToHashSet();
foreach (var c in cache)
    if (!dstCacheIds.Contains(c.IllustrationId)) dst.CardMetadataCache.Add(c);

await dst.SaveChangesAsync();

Console.WriteLine();
Console.WriteLine("Done.");
return 0;

static string? GetArg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string Redact(string conn)
{
    var parts = conn.Split(';')
        .Select(p => p.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            ? "Password=***"
            : p);
    return string.Join(';', parts);
}