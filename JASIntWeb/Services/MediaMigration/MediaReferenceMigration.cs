using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;

namespace JASIntWeb.Services.MediaMigration;

public sealed class MediaReferenceMigration(AppDbContext db)
{
    // Explicit catalog-only allowlist. Customer/order data is never scanned or rewritten.
    private static readonly Dictionary<Type, string[]> Fields = new()
    {
        [typeof(Product)] = ["MainImageUrl", "OgImageUrl", "Details", "Description"],
        [typeof(ProductImage)] = ["ImageUrl"],
        [typeof(Brand)] = ["LogoUrl", "OgImageUrl", "IntroText", "BottomSeoCopy", "Description"],
        [typeof(Category)] = ["Icon", "OgImageUrl", "IntroText", "BottomSeoCopy"],
        [typeof(CarouselSlide)] = ["ImageUrl"],
        [typeof(Guide)] = ["FeaturedImageUrl", "ContentHtml", "Summary"],
        [typeof(DeliverySettings)] = ["BanglaQrImageUrl"]
    };
    public string Fingerprint()
    {
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        return MediaMigrationManifest.Hash($"{connection.Host?.ToLowerInvariant()}|{connection.Port}|{connection.Database}");
    }
    public async Task<List<MediaMigrationReference>> ScanAsync(CancellationToken ct = default)
    {
        List<MediaMigrationReference> references = [];
        await Read<Product>(); await Read<ProductImage>(); await Read<Brand>(); await Read<Category>();
        await Read<CarouselSlide>(); await Read<Guide>(); await Read<DeliverySettings>();
        return references;
        async Task Read<T>() where T : class
        {
            var type = typeof(T);
            var table = db.Model.FindEntityType(type)!.GetTableName()!;
            foreach (var row in await db.Set<T>().IgnoreQueryFilters().AsNoTracking().ToListAsync(ct))
            {
                var id = (int)type.GetProperty("Id")!.GetValue(row)!;
                var parent = type == typeof(Product) ? id : type == typeof(ProductImage) ? (int?)type.GetProperty("ProductId")!.GetValue(row) : null;
                foreach (var field in Fields[type])
                    if (type.GetProperty(field)!.GetValue(row) is string value && LegacyMediaUrls.Find(value).Any())
                        references.Add(new(table, field, id, parent, value, value));
            }
        }
    }
    public async Task<int> ApplyAsync(MediaMigrationManifest manifest, bool rollback, CancellationToken ct = default)
    {
        if (manifest.DatabaseFingerprint != Fingerprint()) throw new InvalidOperationException("The manifest belongs to a different database.");
        if (!rollback && manifest.Assets.Any(asset => asset.DestinationUrl is null || asset.VerifiedObjects.Count == 0))
            throw new InvalidOperationException("Every source asset must be copied and verified before applying URLs.");
        if (!rollback && manifest.References.Any(reference => LegacyMediaUrls.Find(reference.After).Any()))
            throw new InvalidOperationException("Reference replacements have not been prepared.");
        var allowed = Fields.SelectMany(pair => pair.Value.Select(field => (Table: db.Model.FindEntityType(pair.Key)!.GetTableName()!, Column: field))).ToHashSet();
        if (manifest.References.Any(reference => !allowed.Contains((reference.Table, reference.Column)))) throw new InvalidDataException("Manifest contains a non-media field.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var productIds = manifest.References.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).Distinct().Order().ToArray();
        foreach (var id in productIds) await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Products\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        HashSet<int> changedProducts = [];
        var changed = 0;
        foreach (var reference in manifest.References.OrderBy(r => r.Table).ThenBy(r => r.Id).ThenBy(r => r.Column))
        {
            var expected = rollback ? reference.After : reference.Before;
            var replacement = rollback ? reference.Before : reference.After;
            if (expected == replacement) continue;
            var table = reference.Table; var column = reference.Column;
            // Identifiers come exclusively from the EF allowlist above; all values remain parameters.
            #pragma warning disable EF1002 // Table and column are checked against the EF media allowlist; values are parameters.
            var updated = await db.Database.ExecuteSqlRawAsync($"UPDATE \"{table}\" SET \"{column}\" = @replacement WHERE \"Id\" = @id AND \"{column}\" = @expected",
                [new NpgsqlParameter("replacement", replacement), new NpgsqlParameter("id", reference.Id), new NpgsqlParameter("expected", expected)], ct);
            #pragma warning restore EF1002
            if (updated == 0)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = $"SELECT \"{column}\" FROM \"{table}\" WHERE \"Id\" = @id";
                command.Parameters.Add(new NpgsqlParameter("id", reference.Id));
                if (await command.ExecuteScalarAsync(ct) as string != replacement)
                    throw new InvalidOperationException($"Concurrent media edit detected in {table}, row {reference.Id}. No changes applied.");
                continue;
            }
            changed++;
            if (reference.ProductId is { } productId) changedProducts.Add(productId);
        }
        foreach (var id in changedProducts) await db.Database.ExecuteSqlAsync($"UPDATE \"Products\" SET \"CatalogRevision\" = \"CatalogRevision\" + 1 WHERE \"Id\" = {id}", ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return changed;
    }
}
