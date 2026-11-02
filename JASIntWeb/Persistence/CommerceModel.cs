using NpgsqlTypes;
namespace JASIntWeb.Persistence;
// Additive model changes for the commerce phase.

internal static class CommerceModel
{
    public static void Configure(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");
        b.Entity<Product>().HasIndex(x => x.Name).HasMethod("GIN").HasOperators("gin_trgm_ops");
        b.Entity<ProductVariant>()
            .HasIndex(x => x.ProductId)
            .IsUnique()
            .HasFilter("\"IsDefault\" AND \"IsActive\" AND NOT \"IsDeleted\"");
        b.Entity<Product>().Property<NpgsqlTsVector>("SearchVector").HasComputedColumnSql(
            "setweight(to_tsvector('simple', coalesce(\"Name\", '')), 'A') || setweight(to_tsvector('simple', coalesce(\"Description\", '')), 'B') || setweight(to_tsvector('simple', coalesce(\"Details\", '')), 'C')", stored: true);
        b.Entity<Product>().HasIndex("SearchVector").HasMethod("GIN");
        b.Entity<CarouselSlide>().HasIndex(x => new
            {
                x.IsActive,
                x.SortOrder
            });
        b.Entity<CartMergeReceipt>().HasIndex(x => new
            {
                x.UserId,
                x.Token
            })
            .IsUnique();
        b.Entity<Order>().HasIndex(x => x.PublicToken).IsUnique();
        b.Entity<Order>().HasIndex(x => x.CheckoutToken).IsUnique();
        b.Entity<Cart>().HasIndex(x => x.UserId).IsUnique().HasFilter("\"Status\" = 0 AND \"UserId\" IS NOT NULL");
        b.Entity<InventoryItem>().HasIndex(x => x.ProductVariantId).IsUnique();
        b.Entity<AttributeOption>().HasAlternateKey(x => new
            {
                x.Id,
                x.AttributeDefinitionId
            });
        b.Entity<AttributeOption>().HasIndex(x => new
            {
                x.AttributeDefinitionId,
                x.Code
            })
            .IsUnique();
        b.Entity<DeliveryAddress>().HasOne<User>().WithMany(x => x.DeliveryAddresses)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Category>().HasOne(x => x.ParentCategory).WithMany(x => x.ChildCategories)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<OrderItem>()
            .HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CartItem>()
            .ToTable(t => t.HasCheckConstraint("CK_CartItem_Quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 99"));
        b.Entity<InventoryItem>()
            .ToTable(t => t.HasCheckConstraint("CK_Inventory_Quantity",
                "\"QuantityOnHand\" >= 0 AND \"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\""));
        b.Entity<ProductVariant>()
            .ToTable(t => t.HasCheckConstraint("CK_Variant_Price", "\"SellingPrice\" >= 0 AND \"CostPrice\" >= 0"));
        b.Entity<Product>().HasIndex(x => new { x.IsDeleted, x.CategoryId, x.CreatedAt });
        b.Entity<Product>().HasIndex(x => new { x.IsDeleted, x.BrandId, x.CreatedAt });
        b.Entity<Coupon>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Coupon>().HasIndex(x => new { x.IsActive, x.ExpiryDate });
        b.Entity<Order>().HasIndex(x => x.PaymentReferenceCode);
        b.Entity<Order>().HasIndex(x => x.TransactionId);
    }
}
