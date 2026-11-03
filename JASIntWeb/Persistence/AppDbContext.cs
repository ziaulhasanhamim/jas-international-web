using JASIntWeb.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Category = JASIntWeb.Persistence.Entities.Category;
namespace JASIntWeb.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
: IdentityDbContext<User, IdentityRole<int>, int>(options)
{
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ConfigureCatalog(builder);
        ConfigureProductVariants(builder);
        ConfigureInventory(builder);
        ConfigureCart(builder);
        ConfigureOrders(builder);
        ConfigurePayments(builder);
        CommerceModel.Configure(builder);
        ProductAttributeModel.Configure(builder);
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
