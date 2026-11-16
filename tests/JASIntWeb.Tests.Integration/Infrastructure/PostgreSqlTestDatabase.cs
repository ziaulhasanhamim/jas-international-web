using JASIntWeb.Persistence;
using JASIntWeb.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace JASIntWeb.Tests.Integration.Infrastructure;

public static class PostgreSqlTestDatabase
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static PostgreSqlContainer? _container;
    private static Respawner? _respawner;
    private static bool _initialized;

    public static string ConnectionString { get; private set; } = string.Empty;

    public static async Task InitializeAsync()
    {
        if (_initialized) return;

        await Lock.WaitAsync();
        try
        {
            if (_initialized) return;

            var testDbUrl = Environment.GetEnvironmentVariable("TEST_DB_URL");
            if (!string.IsNullOrWhiteSpace(testDbUrl))
            {
                ConnectionString = testDbUrl;
            }
            else
            {
                _container = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("jasintweb_test")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
            }

            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    SchemasToInclude = ["public"],
                    TablesToIgnore = [new Table("__EFMigrationsHistory")]
                });
            }

            await ResetCoreAsync();
            _initialized = true;
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task ResetAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
            return;
        }

        await Lock.WaitAsync();
        try
        {
            await ResetCoreAsync();
        }
        finally
        {
            Lock.Release();
        }
    }

    private static async Task ResetCoreAsync()
    {
        if (_respawner is null) return;

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    FOR r IN (SELECT sequence_name FROM information_schema.sequences WHERE sequence_schema = 'public') LOOP
                        EXECUTE 'ALTER SEQUENCE ' || quote_ident(r.sequence_name) || ' RESTART WITH 1';
                    END LOOP;
                END $$;";
            await cmd.ExecuteNonQueryAsync();
        }

        await SeedBaseDataAsync();
    }

    private static async Task SeedBaseDataAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);

        // Seed Roles
        var adminRole = new IdentityRole<int>("Admin") { Id = 1, NormalizedName = "ADMIN" };
        var customerRole = new IdentityRole<int>("Customer") { Id = 2, NormalizedName = "CUSTOMER" };
        db.Roles.AddRange(adminRole, customerRole);

        // Seed Users
        var hasher = new PasswordHasher<User>();
        var adminUser = new User
        {
            Id = 1,
            UserName = "admin@jasinternational.com.bd",
            NormalizedUserName = "ADMIN@JASINTERNATIONAL.COM.BD",
            Email = "admin@jasinternational.com.bd",
            NormalizedEmail = "ADMIN@JASINTERNATIONAL.COM.BD",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("D"),
            ConcurrencyStamp = Guid.NewGuid().ToString("D")
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin@12345!");

        var customerUser = new User
        {
            Id = 2,
            UserName = "customer@example.com",
            NormalizedUserName = "CUSTOMER@EXAMPLE.COM",
            Email = "customer@example.com",
            NormalizedEmail = "CUSTOMER@EXAMPLE.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("D"),
            ConcurrencyStamp = Guid.NewGuid().ToString("D")
        };
        customerUser.PasswordHash = hasher.HashPassword(customerUser, "Customer@12345!");

        db.Users.AddRange(adminUser, customerUser);
        db.UserRoles.Add(new IdentityUserRole<int> { UserId = 1, RoleId = 1 });

        // Seed Default Delivery Settings
        var delivery = new DeliverySettings
        {
            Id = 1,
            HubDistrict = "Khulna",
            HubCity = "Khulna Sadar",
            HubCityFee = 60m,
            OutsideFee = 120m,
            FreeDeliveryThreshold = null,
            BkashNumber = "01711465078",
            NagadNumber = "01711465078",
            BanglaQrImageUrl = "/images/bangla-qr.png",
            RequireAdvanceForCOD = false,
            AdvanceAmount = 150m
        };
        db.DeliverySettings.Add(delivery);

        // Seed Default Categories
        var catPower = new Category
        {
            Id = 1,
            Name = "Power & Chargers",
            Slug = "power-charging",
            IsDeleted = false
        };
        var catAudio = new Category
        {
            Id = 2,
            Name = "Audio & Earbuds",
            Slug = "audio-earbuds",
            IsDeleted = false
        };
        var catWatch = new Category
        {
            Id = 3,
            Name = "Smart Watches",
            Slug = "smartwatches",
            IsDeleted = false
        };
        db.Categories.AddRange(catPower, catAudio, catWatch);

        // Seed Category Placement
        db.CategoryPlacements.Add(new CategoryPlacement
        {
            Id = 1,
            CategoryId = 1,
            PlacementType = CategoryPlacementType.HomepageFeatured,
            SortOrder = 1,
            IsActive = true
        });

        // Seed Default Brands
        var brandAnker = new Brand
        {
            Id = 1,
            Name = "Anker",
            Slug = "anker",
            LogoUrl = "/images/brands/anker.png",
            IsFeatured = true
        };
        var brandOraimo = new Brand
        {
            Id = 2,
            Name = "oraimo",
            Slug = "oraimo",
            LogoUrl = "/images/brands/oraimo.png",
            IsFeatured = true
        };
        var brandApple = new Brand
        {
            Id = 3,
            Name = "Apple",
            Slug = "apple",
            LogoUrl = "/images/brands/apple.png",
            IsFeatured = true
        };
        db.Brands.AddRange(brandAnker, brandOraimo, brandApple);

        // Seed Default Product
        var product = new Product
        {
            Id = 1,
            Name = "Anker 737 Power Bank",
            Slug = "anker-737-power-bank",
            BrandId = 1,
            CategoryId = 1,
            Details = "<h3>High Power 140W</h3><p>24,000mAh portable charger</p>",
            Description = "Premium power bank for laptops and phones.",
            MainImageUrl = "/images/products/anker-737.png",
            IsFeatured = true,
            IsDeleted = false
        };

        var product2 = new Product
        {
            Id = 2,
            Name = "oraimo BoomPop2 OHP-610 Head Phone",
            Slug = "oraimo-boompop2-ohp-610-head-phone",
            BrandId = 2,
            CategoryId = 2,
            Details = "<h3>oraimo BoomPop2</h3><p>Wireless Over-Ear Headphones with Heavy Bass</p>",
            Description = "Rich sound, deep bass, long battery life.",
            MainImageUrl = "/images/products/oraimo-boompop2.png",
            IsFeatured = true,
            IsDeleted = false
        };
        db.Products.AddRange(product, product2);

        // Seed Default Product Variants
        var variant = new ProductVariant
        {
            Id = 1,
            ProductId = 1,
            SKU = "ANKER-737-STD",
            CostPrice = 10000m,
            SellingPrice = 14500m,
            IsActive = true,
            IsDefault = true,
            IsDeleted = false
        };

        var variant2 = new ProductVariant
        {
            Id = 2,
            ProductId = 2,
            SKU = "ORA-BP2-001",
            CostPrice = 2500m,
            SellingPrice = 3200m,
            IsActive = true,
            IsDefault = true,
            IsDeleted = false
        };
        db.ProductVariants.AddRange(variant, variant2);

        // Seed Inventory
        var inventory = new InventoryItem
        {
            Id = 1,
            ProductVariantId = 1,
            QuantityOnHand = 50,
            QuantityReserved = 0
        };

        var inventory2 = new InventoryItem
        {
            Id = 2,
            ProductVariantId = 2,
            QuantityOnHand = 30,
            QuantityReserved = 0
        };
        db.InventoryItems.AddRange(inventory, inventory2);

        await db.SaveChangesAsync();

        // Advance PostgreSQL identity sequences so subsequent inserts do not collide
        await db.Database.ExecuteSqlRawAsync(@"
            SELECT setval(pg_get_serial_sequence('""AspNetRoles""', 'Id'), coalesce(max(""Id""), 1)) FROM ""AspNetRoles"";
            SELECT setval(pg_get_serial_sequence('""AspNetUsers""', 'Id'), coalesce(max(""Id""), 1)) FROM ""AspNetUsers"";
            SELECT setval(pg_get_serial_sequence('""DeliverySettings""', 'Id'), coalesce(max(""Id""), 1)) FROM ""DeliverySettings"";
            SELECT setval(pg_get_serial_sequence('""Categories""', 'Id'), coalesce(max(""Id""), 1)) FROM ""Categories"";
            SELECT setval(pg_get_serial_sequence('""CategoryPlacements""', 'Id'), coalesce(max(""Id""), 1)) FROM ""CategoryPlacements"";
            SELECT setval(pg_get_serial_sequence('""Brands""', 'Id'), coalesce(max(""Id""), 1)) FROM ""Brands"";
            SELECT setval(pg_get_serial_sequence('""Products""', 'Id'), coalesce(max(""Id""), 1)) FROM ""Products"";
            SELECT setval(pg_get_serial_sequence('""ProductVariants""', 'Id'), coalesce(max(""Id""), 1)) FROM ""ProductVariants"";
            SELECT setval(pg_get_serial_sequence('""InventoryItems""', 'Id'), coalesce(max(""Id""), 1)) FROM ""InventoryItems"";
        ");
    }
}
