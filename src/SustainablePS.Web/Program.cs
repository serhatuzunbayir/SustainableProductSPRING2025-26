using Microsoft.EntityFrameworkCore;
using SustainablePS.Core.Data;
using SustainablePS.Core.Services;
using SustainablePS.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor components ──────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── EF Core + SQLite — relative path, shared with the desktop MAUI app ────
// The same .db file is used by the API and by the MAUI desktop app.
// Changing the path in MarketplaceDbContext.RelativeDbPath is the only
// configuration needed to relocate the database.
builder.Services.AddDbContextFactory<MarketplaceDbContext>(options =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, MarketplaceDbContext.RelativeDbPath);
    options.UseSqlite($"Data Source={dbPath}");
});

// Register the database-backed service as a singleton
builder.Services.AddSingleton<DatabaseMarketplaceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
