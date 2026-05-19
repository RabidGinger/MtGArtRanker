using Microsoft.EntityFrameworkCore;
using MtGArtRanker.Api.Models;

namespace MtGArtRanker.Api.Data;

public class AppDbContext : DbContext
{
    public static readonly Guid DefaultUserId = new("00000000-0000-0000-0000-000000000001");

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Ranking> Rankings => Set<Ranking>();
    public DbSet<RankingItem> RankingItems => Set<RankingItem>();
    public DbSet<CardMetadataCache> CardMetadataCache => Set<CardMetadataCache>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasData(new User
            {
                Id = DefaultUserId,
                DisplayName = "Default User",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        mb.Entity<Ranking>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User)
                .WithMany(u => u.Rankings)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.OracleId }).IsUnique();
        });

        mb.Entity<RankingItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Ranking)
                .WithMany(r => r.Items)
                .HasForeignKey(x => x.RankingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RankingId, x.IllustrationId }).IsUnique();
            e.HasIndex(x => new { x.RankingId, x.Position });
        });

        mb.Entity<CardMetadataCache>(e =>
        {
            e.HasKey(x => x.IllustrationId);
            e.HasIndex(x => x.OracleId);

            // Use the provider-native unbounded text type for the JSON payload.
            e.Property(x => x.Payload).HasColumnType(
                Database.IsSqlServer() ? "nvarchar(max)" : "TEXT");
        });
    }
}
