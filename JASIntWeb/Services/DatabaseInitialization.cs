namespace JASIntWeb.Services;

internal static class DatabaseInitialization
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrations", app.Environment.IsDevelopment())
            && !app.Configuration.GetValue("Database:SkipMigrations", false);
        if (applyMigrations)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // A failed migration must stop startup instead of serving a partially initialized store.
            await db.Database.MigrateAsync();
        }
        if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:Enabled", true))
        {
            await app.Services.SeedTestDataAsync<AppDbContext>(Path.Combine(app.Environment.ContentRootPath, "data", "JasTestSeedData.json"));
            await app.Services.SeedBrandsAsync();
            await app.Services.SeedCommerceEnhancementsAsync();
        }
        await app.Services.SeedAdminAsync(app.Configuration);
        await app.Services.PatchCatalogDataAsync();
    }
}
