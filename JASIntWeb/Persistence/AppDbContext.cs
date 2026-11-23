using JASIntWeb.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Category = JASIntWeb.Persistence.Entities.Category;
namespace JASIntWeb.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
: IdentityDbContext<User, IdentityRole<int>, int>(options), IDataProtectionKeyContext
{
    public DbSet<ProductEditorReceipt> ProductEditorReceipts => Set<ProductEditorReceipt>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<ProductAttributeDefinition> ProductAttributeDefinitions => Set<ProductAttributeDefinition>();
    public DbSet<ProductAttributeOption> ProductAttributeOptions => Set<ProductAttributeOption>();
    public DbSet<Brand> Brands { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
    public DbSet<ProductVariantOption> ProductVariantOptions { get; set; } = null!;
    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;
    public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
    public DbSet<AttributeDefinition> AttributeDefinitions { get; set; } = null!;
    public DbSet<AttributeOption> AttributeOptions { get; set; } = null!;
    public DbSet<ProductImage> ProductImages { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<CategoryPlacement> CategoryPlacements { get; set; } = null!;
    public DbSet<Cart> Carts { get; set; } = null!;
    public DbSet<CartItem> CartItems { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<DeliveryAddress> DeliveryAddresses { get; set; } = null!;
    public DbSet<CarouselSlide> CarouselSlides => Set<CarouselSlide>();
    public DbSet<CartMergeReceipt> CartMergeReceipts => Set<CartMergeReceipt>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<DeliverySettings> DeliverySettings => Set<DeliverySettings>();
    public DbSet<HomepageSection> HomepageSections => Set<HomepageSection>();
    public DbSet<CuratedPromoCard> CuratedPromoCards => Set<CuratedPromoCard>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<OrderTimelineEvent> OrderTimelineEvents => Set<OrderTimelineEvent>();
    public DbSet<UrlRedirect> UrlRedirects => Set<UrlRedirect>();
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<VisitorLog> VisitorLogs => Set<VisitorLog>();

    public DbSet<StockNotification> StockNotifications => Set<StockNotification>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ProductEditorReceipt>(e => { e.HasKey(r => new { r.ActorId, r.OperationId }); e.Property(r => r.ResultJson).HasColumnType("jsonb"); });
        builder.Entity<InventoryItem>().Property(i => i.Version).IsRowVersion();
        builder.Entity<ProductVariant>().Property(v => v.NormalizedSku).HasComputedColumnSql("upper(btrim(\"SKU\"))", stored: true);
        builder.Entity<ProductVariant>().HasIndex(v => v.NormalizedSku).IsUnique().HasDatabaseName("UX_ProductVariants_NormalizedSku");
        builder.Entity<AttributeDefinition>().Property(a => a.MetadataSchemaJson).HasColumnType("jsonb");
        builder.Entity<AttributeOption>().Property(a => a.MetadataJson).HasColumnType("jsonb");
        builder.Entity<ProductAttributeDefinition>().Property(a => a.MetadataSchemaJson).HasColumnType("jsonb");
        builder.Entity<ProductAttributeOption>().Property(a => a.MetadataJson).HasColumnType("jsonb");
        builder.Entity<StockNotification>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.NormalizedEmail).HasMaxLength(254);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.ProductVariantId, x.NormalizedEmail }).IsUnique().HasFilter("\"Status\" IN (0, 1, 4)");
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
        });
        ConfigureCatalog(builder);
        ConfigureProductVariants(builder);
        ConfigureInventory(builder);
        ConfigureCart(builder);
        ConfigureOrders(builder);
        ConfigurePayments(builder);
        CommerceModel.Configure(builder);
        ProductAttributeModel.Configure(builder);
        ConfigureSeoAndRedirects(builder);
        ConfigureVisitorLogs(builder);
    }

    private static void ConfigureVisitorLogs(ModelBuilder builder)
    {
        builder.Entity<VisitorLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.VisitorId);
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.Path);
            e.HasIndex(x => x.ReferrerDomain);
            e.HasIndex(x => x.DeviceType);
            e.HasIndex(x => x.City);
        });
    }

    private static void ConfigureSeoAndRedirects(ModelBuilder builder)
    {
        builder.Entity<UrlRedirect>(e =>
        {
            e.HasIndex(x => x.OldPath).IsUnique();
        });

        builder.Entity<Guide>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.IsPublished, x.PublishedAt });
        });

        builder.Entity<Order>(e =>
        {
            e.HasIndex(x => x.MetaPurchaseSent);
            e.HasIndex(x => x.UtmCampaign);
        });

        builder.Entity<Cart>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CustomerPhone);
        });
    }

    private static void ConfigureOrders(ModelBuilder builder)
    {
        builder.Entity<Order>(e =>
            {
                e.HasIndex(x => new { x.Status, x.DeliveredAt });
                e.HasIndex(x => new
                    {
                        x.UserId,
                        x.CreatedAt
                    });
                e.HasIndex(x => new
                    {
                        x.Status,
                        x.CreatedAt
                    });
            });
        builder.Entity<OrderItem>(e =>
            {
                e.HasIndex(x => x.OrderId);
            });
        builder.Entity<OrderTimelineEvent>(e =>
            {
                e.HasIndex(x => new { x.OrderId, x.CreatedAt });
            });
    }

    static void ConfigurePayments(ModelBuilder builder)
    {
        builder.Entity<Payment>(e =>
            {
                e.HasIndex(x => new
                    {
                        x.OrderId,
                        x.CreatedAt
                    });
                e.HasIndex(x => x.ExternalReference);
            });
    }

    private static void ConfigureCart(ModelBuilder builder)
    {
        builder.Entity<Cart>(e =>
            {
                e.HasIndex(x => x.Token)
                .IsUnique();
                e.HasIndex(x => new
                    {
                        x.UserId,
                        x.Status
                    });
                e.HasIndex(x => x.ExpiresAt);
            });
        builder.Entity<CartItem>(e =>
            {
                e.HasIndex(x => new
                    {
                        x.CartId,
                        x.ProductVariantId
                    })
                .IsUnique();
            });
    }

    static void ConfigureCatalog(ModelBuilder builder)
    {
        builder.Entity<Category>(e =>
            {
                e.HasIndex(x => x.Slug)
                .IsUnique();
            });
        builder.Entity<CategoryPlacement>(e =>
            {
                e.HasIndex(x => new
                    {
                        x.CategoryId,
                        x.PlacementType
                    })
                .IsUnique();
                e.HasIndex(x => new
                    {
                        x.PlacementType,
                        x.IsActive,
                        x.SortOrder
                    });
            });
        builder.Entity<Brand>(e =>
            {
                e.HasIndex(x => x.Slug)
                .IsUnique();
                e.HasIndex(x => new
                    {
                        x.IsFeatured,
                        x.SortOrder
                    });
            });
        builder.Entity<Product>(e =>
            {
                e.HasIndex(x => x.Slug)
                .IsUnique();
                e.HasOne(p => p.Brand)
                    .WithMany(b => b.Products)
                    .HasForeignKey(p => p.BrandId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        builder.Entity<ProductImage>(e =>
            {
                e.HasIndex(x => new
                    {
                        x.ProductId,
                        x.SortOrder
                    });
            });
        builder.Entity<ProductReview>(e =>
            {
                e.HasIndex(x => new { x.ProductId, x.IsApproved });
                e.HasIndex(x => x.CreatedAt);
            });
    }

    static void ConfigureProductVariants(ModelBuilder builder)
    {
        builder.Entity<AttributeDefinition>(e =>
            {
                e.HasIndex(x => x.Code)
                .IsUnique();
            });
        builder.Entity<AttributeOption>(e =>
            {
                e.HasIndex(x => new
                    {
                        x.AttributeDefinitionId,
                        x.Value
                    })
                .IsUnique();
            });
        builder.Entity<ProductVariant>(e =>
            {
                e.HasIndex(x => x.SKU)
                .IsUnique();
                e.HasIndex(x => x.Barcode)
                .IsUnique();
                e.HasIndex(x => new
                    {
                        x.ProductId,
                        x.IsActive
                    });
            });
    }

    static void ConfigureInventory(ModelBuilder builder)
    {
        builder.Entity<InventoryTransaction>(e =>
            {
                e.HasIndex(x => new
                    {
                        x.ProductVariantId,
                        x.CreatedAt
                    });
                e.HasIndex(x => x.CreatedAt);
            });
    }
}
