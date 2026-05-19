using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MtGArtRanker.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardMetadataCache",
                columns: table => new
                {
                    IllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OracleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardMetadataCache", x => x.IllustrationId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rankings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OracleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rankings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rankings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RankingItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RankingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScryfallCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtCropUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NormalImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScryfallUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ArtistName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SetCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankingItems_Rankings_RankingId",
                        column: x => x.RankingId,
                        principalTable: "Rankings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DisplayName" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Default User" });

            migrationBuilder.CreateIndex(
                name: "IX_CardMetadataCache_OracleId",
                table: "CardMetadataCache",
                column: "OracleId");

            migrationBuilder.CreateIndex(
                name: "IX_RankingItems_RankingId_IllustrationId",
                table: "RankingItems",
                columns: new[] { "RankingId", "IllustrationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingItems_RankingId_Position",
                table: "RankingItems",
                columns: new[] { "RankingId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Rankings_UserId_OracleId",
                table: "Rankings",
                columns: new[] { "UserId", "OracleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardMetadataCache");

            migrationBuilder.DropTable(
                name: "RankingItems");

            migrationBuilder.DropTable(
                name: "Rankings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
