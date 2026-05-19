using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using MtGArtRanker.Api.Data;
using MtGArtRanker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Key Vault (only when configured; uses Managed Identity in Azure, dev creds locally)
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// EF Core — provider is config-driven so local dev can use a SQLite file
// while Azure runs SQL Server.
var dbProvider = (builder.Configuration["Database:Provider"] ?? "sqlserver").ToLowerInvariant();
var connectionString =
    builder.Configuration.GetConnectionString("Sql")
    ?? builder.Configuration["Sql-ConnectionString"]
    ?? (dbProvider == "sqlite"
        ? "Data Source=mtgartranker.db"
        : "Server=(localdb)\\MSSQLLocalDB;Database=MtGArtRanker;Trusted_Connection=True;TrustServerCertificate=True");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (dbProvider == "sqlite") opt.UseSqlite(connectionString);
    else opt.UseSqlServer(connectionString);
});

// Scryfall HTTP client — required User-Agent + Accept per Scryfall API guidelines
builder.Services.AddHttpClient<IScryfallClient, ScryfallClient>(c =>
{
    c.BaseAddress = new Uri("https://api.scryfall.com/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("MtGArtRanker/0.1 (+https://github.com/)");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddScoped<IRankingService, RankingService>();

// CORS — allow SWA origin in prod via config; permissive in dev
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Initialize the database. SQLite uses EnsureCreated (good enough for local
// scratch storage — when migrating to live SQL the EF migrations are applied
// instead). SQL Server uses real migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        if (db.Database.IsSqlite()) db.Database.EnsureCreated();
        else db.Database.Migrate();
    }
}

app.Run();