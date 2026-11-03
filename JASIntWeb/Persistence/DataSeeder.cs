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
            if (o.Status == OrderStatus.Delivered)
            {
                o.DeliveredAt ??= o.UpdatedAt ?? o.CreatedAt;
            }
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
        var variantCosts = data.ProductVariants.ToDictionary(v => v.Id, v => v.CostPrice);
        // Fixture order costs were not recorded at sale time; preserve them as explicit estimates.
        db.AddRange(data.OrderItems.Select(i => new OrderItem
        {
            Id = i.Id,
            OrderId = i.OrderId,
            ProductVariantId = i.ProductVariantId,
            ProductName = i.ProductName,
            VariantName = i.VariantName,
            UnitPrice = i.UnitPrice,
            UnitCost = variantCosts[i.ProductVariantId],
            CostIsEstimated = true,
            Quantity = i.Quantity,
            LineTotal = i.LineTotal,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            CreatedByUserId = i.CreatedByUserId,
            UpdatedByUserId = i.UpdatedByUserId
        }));
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

    public static async Task SeedBrandsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Brands.AnyAsync(ct))
        {
            var initialBrands = new List<Brand>
            {
                new() { Name = "Apple", Slug = "apple", IsFeatured = true, SortOrder = 1 },
                new() { Name = "Samsung", Slug = "samsung", IsFeatured = true, SortOrder = 2 },
                new() { Name = "Google Pixel", Slug = "google", IsFeatured = true, SortOrder = 3 },
                new() { Name = "Xiaomi", Slug = "xiaomi", IsFeatured = true, SortOrder = 4 },
                new() { Name = "Anker", Slug = "anker", IsFeatured = true, SortOrder = 5 },
                new() { Name = "Sony", Slug = "sony", IsFeatured = true, SortOrder = 6 },
                new() { Name = "OnePlus", Slug = "oneplus", IsFeatured = true, SortOrder = 7 },
                new() { Name = "Baseus", Slug = "baseus", IsFeatured = true, SortOrder = 8 },
                new() { Name = "JBL", Slug = "jbl", IsFeatured = true, SortOrder = 9 }
            };
            db.Brands.AddRange(initialBrands);
            await db.SaveChangesAsync(ct);
        }

        // Link any unassigned products to matching brands
        var brands = await db.Brands.Where(b => !b.IsDeleted).ToListAsync(ct);
        var unlinkedProducts = await db.Products.Where(p => p.BrandId == null && !p.IsDeleted).ToListAsync(ct);
        if (unlinkedProducts.Count > 0 && brands.Count > 0)
        {
            foreach (var p in unlinkedProducts)
            {
                var lower = p.Name.ToLowerInvariant();
                Brand? matched = null;
                if (lower.Contains("iphone") || lower.Contains("macbook") || lower.Contains("airpods") || lower.Contains("apple"))
                    matched = brands.FirstOrDefault(b => b.Slug == "apple");
                else if (lower.Contains("galaxy") || lower.Contains("samsung"))
                    matched = brands.FirstOrDefault(b => b.Slug == "samsung");
                else if (lower.Contains("pixel") || lower.Contains("google"))
                    matched = brands.FirstOrDefault(b => b.Slug == "google");
                else if (lower.Contains("xiaomi") || lower.Contains("redmi") || lower.Contains("poco"))
                    matched = brands.FirstOrDefault(b => b.Slug == "xiaomi");
                else if (lower.Contains("anker") || lower.Contains("soundcore"))
                    matched = brands.FirstOrDefault(b => b.Slug == "anker");
                else if (lower.Contains("sony") || lower.Contains("wh-1000") || lower.Contains("wf-1000"))
                    matched = brands.FirstOrDefault(b => b.Slug == "sony");
                else if (lower.Contains("oneplus") || lower.Contains("nord"))
                    matched = brands.FirstOrDefault(b => b.Slug == "oneplus");
                else if (lower.Contains("baseus"))
                    matched = brands.FirstOrDefault(b => b.Slug == "baseus");
                else if (lower.Contains("jbl"))
                    matched = brands.FirstOrDefault(b => b.Slug == "jbl");

                if (matched is not null)
                {
                    p.BrandId = matched.Id;
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task SeedCommerceEnhancementsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.DeliverySettings.AnyAsync(ct))
        {
            db.DeliverySettings.Add(new DeliverySettings
            {
                HubDistrict = "Khulna",
                HubCity = "Khulna Sadar",
                HubCityFee = 50m,
                OutsideFee = 150m,
                FreeDeliveryThreshold = 5000m,
                RequireAdvanceForCOD = true,
                AdvanceAmount = 150m,
                BkashNumber = "01711465078",
                NagadNumber = "01711465078",
                BanglaQrImageUrl = "/jas-logo.png",
                PaymentInstructions = "Send ৳150 Advance Delivery Fee to our bKash/Nagad number or scan Bangla QR with the Order Reference code to instantly confirm your order.",
                SmsWebhookSecret = "jas_secret_sms_2026",
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Coupons.AnyAsync(ct))
        {
            db.Coupons.AddRange(
                new Coupon
                {
                    Code = "WELCOME200",
                    DiscountType = CouponDiscountType.FixedAmount,
                    DiscountValue = 200m,
                    MinOrderAmount = 1000m,
                    IsActive = true,
                    Description = "৳200 flat discount on orders over ৳1,000"
                },
                new Coupon
                {
                    Code = "SAVE10",
                    DiscountType = CouponDiscountType.Percentage,
                    DiscountValue = 10m,
                    MaxDiscountAmount = 500m,
                    MinOrderAmount = 500m,
                    IsActive = true,
                    Description = "10% off up to ৳500"
                },
                new Coupon
                {
                    Code = "FREESHIP",
                    DiscountType = CouponDiscountType.FreeDelivery,
                    DiscountValue = 0m,
                    MinOrderAmount = 2000m,
                    IsActive = true,
                    Description = "Free delivery on orders over ৳2,000"
                }
            );
            await db.SaveChangesAsync(ct);
        }

        if (!await db.HomepageSections.AnyAsync(ct))
        {
            db.HomepageSections.AddRange(
                new HomepageSection
                {
                    Title = "Sound Surround",
                    Eyebrow = "HEAR MORE",
                    CategorySlug = "audio",
                    ViewAllHref = "/category/audio",
                    SortOrder = 1,
                    IsActive = true,
                    ItemLimit = 8
                },
                new HomepageSection
                {
                    Title = "New Arrivals",
                    Eyebrow = "JUST LANDED",
                    CategorySlug = null,
                    ViewAllHref = "/products?sort=newest",
                    SortOrder = 2,
                    IsActive = true,
                    ItemLimit = 8
                },
                new HomepageSection
                {
                    Title = "Smart Watches & Bands",
                    Eyebrow = "STAY CONNECTED",
                    CategorySlug = "wearables",
                    ViewAllHref = "/category/wearables",
                    SortOrder = 3,
                    IsActive = true,
                    ItemLimit = 8
                },
                new HomepageSection
                {
                    Title = "Wireless Speakers",
                    Eyebrow = "TURN IT UP",
                    CategorySlug = "speakers",
                    ViewAllHref = "/category/speakers",
                    SortOrder = 4,
                    IsActive = true,
                    ItemLimit = 8
                },
                new HomepageSection
                {
                    Title = "Power & Fast Chargers",
                    Eyebrow = "DAILY ESSENTIALS",
                    CategorySlug = "power-charging",
                    ViewAllHref = "/category/power-charging",
                    SortOrder = 5,
                    IsActive = true,
                    ItemLimit = 8
                }
            );
            await db.SaveChangesAsync(ct);
        }

        var productsWithoutCompare = await db.Products.Include(p => p.ProductVariants).Where(p => p.CompareAtPrice == null).ToListAsync(ct);
        if (productsWithoutCompare.Count > 0)
        {
            foreach (var p in productsWithoutCompare)
            {
                var price = p.ProductVariants.FirstOrDefault()?.SellingPrice ?? 0;
                if (price > 0)
                {
                    p.CompareAtPrice = Math.Round((price * 1.08m) / 100m) * 100m;
                    if (p.CompareAtPrice <= price) p.CompareAtPrice = price + 500m;
                }
            }
            await db.SaveChangesAsync(ct);
        }

        // Upgrade any existing placeholder images or outdated slides to authentic Gadget & Gear imagery
        var hasPlaceholders = await db.Products.AnyAsync(p => p.MainImageUrl != null && p.MainImageUrl.Contains("placehold.co"), ct);
        var hasOldSlides = await db.CarouselSlides.AnyAsync(s => s.Title != null && (s.Title.Contains("Your tech") || s.Title.Contains("Find your everyday")), ct);
        if (hasPlaceholders || hasOldSlides)
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "data", "JasTestSeedData.json");
            if (!File.Exists(seedPath))
            {
                seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "JasTestSeedData.json");
            }
            if (File.Exists(seedPath))
            {
                var settings = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
                settings.Converters.Add(new JsonStringEnumConverter());
                var seedData = JsonSerializer.Deserialize<SeedData>(await File.ReadAllTextAsync(seedPath, ct), settings);
                if (seedData != null)
                {
                    if (hasPlaceholders)
                    {
                        var productMap = seedData.Products.ToDictionary(p => p.Id, p => p.MainImageUrl);
                        var productsToUpdate = await db.Products.Where(p => p.MainImageUrl.Contains("placehold.co")).ToListAsync(ct);
                        foreach (var p in productsToUpdate)
                        {
                            if (productMap.TryGetValue(p.Id, out var newUrl))
                            {
                                p.MainImageUrl = newUrl;
                            }
                        }

                        var imagesToUpdate = await db.ProductImages.Where(i => i.ImageUrl.Contains("placehold.co")).ToListAsync(ct);
                        var imgMap = seedData.ProductImages.ToDictionary(i => i.Id, i => i.ImageUrl);
                        foreach (var img in imagesToUpdate)
                        {
                            if (imgMap.TryGetValue(img.Id, out var newUrl))
                            {
                                img.ImageUrl = newUrl;
                            }
                            else if (productMap.TryGetValue(img.ProductId, out var fallbackUrl))
                            {
                                img.ImageUrl = fallbackUrl;
                            }
                        }
                    }

                    if (hasOldSlides && seedData.CarouselSlides.Count > 0)
                    {
                        var oldSlides = await db.CarouselSlides.ToListAsync(ct);
                        db.CarouselSlides.RemoveRange(oldSlides);
                        try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Concurrently updated by another process/test worker
                    }
                        db.CarouselSlides.AddRange(seedData.CarouselSlides);
                    }

                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Concurrently updated by another process/test worker
                    }
                }
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
