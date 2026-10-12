using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
namespace JASIntWeb.Persistence;

public static class JsonTestDataSeeder
{
    public static async Task SeedTestDataAsync<TContext>(this IServiceProvider services, string jsonPath, CancellationToken ct = default) where TContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        // Fixtures are for an empty development catalog only. Never overwrite a working catalog.
        if (await db.Set<Product>().AnyAsync(ct))
        {
            return;
        }
        var settings = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        settings.Converters.Add(new JsonStringEnumConverter());
        var data = JsonSerializer.Deserialize<SeedData>(await File.ReadAllTextAsync(jsonPath, ct), settings) ?? throw new InvalidOperationException("Invalid seed data.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.AddRange(data.AttributeDefinitions);
        await db.SaveChangesAsync(ct);
        db.AddRange(data.AttributeOptions);
        await db.SaveChangesAsync(ct);
        // Insert roots and descendants in dependency order; no self-parenting sentinel category.
        var pending = data.Categories.ToList();
        var inserted = new HashSet<int>();
        while (pending.Count > 0)
        {
            var batch = pending.Where(c => c.ParentCategoryId is null || inserted.Contains(c.ParentCategoryId.Value))
                .ToList();
            if (batch.Count == 0)
            {
                throw new InvalidOperationException("Seed categories contain a cycle or missing parent.");
            }
            db.AddRange(batch);
            await db.SaveChangesAsync(ct);
            foreach (var c in batch)
            {
                inserted.Add(c.Id);
                pending.Remove(c);
            }
        }
        db.AddRange(data.CategoryPlacements);
        db.AddRange(data.Products);
        await db.SaveChangesAsync(ct);
        db.AddRange(data.ProductAttributeDefinitions);
        await db.SaveChangesAsync(ct);
        db.AddRange(data.ProductAttributeOptions);
        await db.SaveChangesAsync(ct);
        db.AddRange(data.ProductImages);
        db.AddRange(data.ProductVariants);
        await db.SaveChangesAsync(ct);
        db.AddRange(data.ProductVariantOptions);
        db.AddRange(data.InventoryItems);
        db.AddRange(data.InventoryTransactions);
        await db.SaveChangesAsync(ct);
        var userIds = new Dictionary<int, int>();
        foreach (var demo in data.Users)
        {
            var existing = await users.FindByEmailAsync(demo.Email);
            if (existing is not null)
            {
                userIds[demo.Id] = existing.Id;
                continue;
            }
            var user = new User
            {
                UserName = demo.Email,
                Email = demo.Email,
                PhoneNumber = demo.PhoneNumber,
                EmailConfirmed = true
            };
            var result = await users.CreateAsync(user, demo.Password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
            userIds[demo.Id] = user.Id;
        }
        foreach (var a in data.DeliveryAddresses)
        {
            if (a.CustomerId.HasValue)
            {
                a.CustomerId = userIds[a.CustomerId.Value];
            }
        }
        foreach (var c in data.Carts)
        {
            if (c.UserId.HasValue)
            {
                c.UserId = userIds[c.UserId.Value];
            }
        }
        foreach (var o in data.Orders)
        {
            if (o.UserId.HasValue)
            {
                o.UserId = userIds[o.UserId.Value];
            }
        }
        db.AddRange(data.DeliveryAddresses);
        db.AddRange(data.Carts);
        db.AddRange(data.Orders);
        db.AddRange(data.CarouselSlides);
        await db.SaveChangesAsync(ct);
        db.AddRange(data.CartItems);
        db.AddRange(data.OrderItems);
        db.AddRange(data.Payments);
        await db.SaveChangesAsync(ct);
        // Explicit fixture IDs do not advance PostgreSQL sequences. Repair EVERY identity sequence.
        await db.Database.ExecuteSqlRawAsync("""
            DO $block$
            DECLARE r record; seq text; maximum bigint;
            BEGIN
                FOR r IN SELECT table_name FROM information_schema.columns WHERE table_schema = 'public' AND column_name = 'Id' AND is_identity = 'YES' LOOP
                    seq := pg_get_serial_sequence(format('%I.%I', 'public', r.table_name), 'Id');
                    EXECUTE format('SELECT MAX(%I) FROM %I.%I', 'Id', 'public', r.table_name) INTO maximum;
                    PERFORM setval(seq::regclass, COALESCE(maximum, 1), maximum IS NOT NULL);
                END LOOP;
            END $block$;
            """, ct);
        await tx.CommitAsync(ct);
    }

    public static async Task SeedAdminAsync(this IServiceProvider services, IConfiguration config)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        if (!await roles.RoleExistsAsync("Admin"))
        {
            var result = await roles.CreateAsync(new IdentityRole<int>("Admin"));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }
        if (!await users.IsInRoleAsync(user, "Admin"))
        {
            var result = await users.AddToRoleAsync(user, "Admin");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private sealed class SeedData
    {
        public List<AttributeDefinition> AttributeDefinitions { get; set; } = [];
        public List<AttributeOption> AttributeOptions { get; set; } = [];
        public List<Category> Categories { get; set; } = [];
        public List<CategoryPlacement> CategoryPlacements { get; set; } = [];
        public List<ProductAttributeDefinition> ProductAttributeDefinitions { get; set; } = [];
        public List<ProductAttributeOption> ProductAttributeOptions { get; set; } = [];
        public List<Product> Products { get; set; } = [];
        public List<ProductImage> ProductImages { get; set; } = [];
        public List<ProductVariant> ProductVariants { get; set; } = [];
        public List<ProductVariantOption> ProductVariantOptions { get; set; } = [];
        public List<InventoryItem> InventoryItems { get; set; } = [];
        public List<InventoryTransaction> InventoryTransactions { get; set; } = [];
        public List<SeedUser> Users { get; set; } = [];
        public List<DeliveryAddress> DeliveryAddresses { get; set; } = [];
        public List<Cart> Carts { get; set; } = [];
        public List<CartItem> CartItems { get; set; } = [];
        public List<Order> Orders { get; set; } = [];
        public List<OrderItem> OrderItems { get; set; } = [];
        public List<Payment> Payments { get; set; } = [];
        public List<CarouselSlide> CarouselSlides { get; set; } = [];
    }

    private sealed record SeedUser(int Id, string Email, string? PhoneNumber, string Password);
}
