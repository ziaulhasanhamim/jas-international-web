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
        b.Entity<Coupon>().Property(x => x.IncludedCategoryIds).HasDefaultValueSql("'{}'");
        b.Entity<Coupon>().Property(x => x.ExcludedCategoryIds).HasDefaultValueSql("'{}'");
        b.Entity<Coupon>().Property(x => x.IncludedBrandIds).HasDefaultValueSql("'{}'");
        b.Entity<Coupon>().Property(x => x.ExcludedBrandIds).HasDefaultValueSql("'{}'");
        b.Entity<Coupon>().Property(x => x.IncludedTagIds).HasDefaultValueSql("'{}'");
        b.Entity<Coupon>().Property(x => x.ExcludedTagIds).HasDefaultValueSql("'{}'");
        b.Entity<Order>().HasIndex(x => x.PaymentReferenceCode);
        b.Entity<Order>().HasIndex(x => x.TransactionId);
        b.Entity<ProductTag>().HasKey(x => new { x.ProductId, x.TagId });
        b.Entity<ProductTag>()
            .HasOne(x => x.Product)
            .WithMany(x => x.ProductTags)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductTag>()
            .HasOne(x => x.Tag)
            .WithMany(x => x.ProductTags)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Tag>().HasIndex(x => x.Slug).IsUnique().HasFilter("NOT \"IsDeleted\"");
        b.Entity<Campaign>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Campaign>().HasIndex(x => new { x.IsActive, x.SortOrder });
        b.Entity<Campaign>().Property(x => x.CuratedProductIds).HasDefaultValueSql("'{}'");
        b.Entity<Campaign>()
            .HasOne(x => x.Collection)
            .WithMany()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Campaign>()
            .HasOne(x => x.Coupon)
            .WithMany()
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<CampaignProduct>().HasIndex(x => new { x.CampaignId, x.ProductId }).IsUnique().HasFilter("\"ProductVariantId\" IS NULL");
        b.Entity<CampaignProduct>().HasIndex(x => new { x.CampaignId, x.ProductVariantId }).IsUnique().HasFilter("\"ProductVariantId\" IS NOT NULL");
        b.Entity<CampaignProduct>().HasIndex(x => new { x.CampaignId, x.SortOrder });
        b.Entity<CampaignProduct>()
            .HasOne(x => x.Campaign)
            .WithMany(c => c.CampaignProducts)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CampaignProduct>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CampaignProduct>()
            .HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Coupon>()
            .HasOne(x => x.Collection)
            .WithMany()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<HomepageSection>()
            .HasOne(x => x.Collection)
            .WithMany()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Collection>().HasIndex(x => x.Slug).IsUnique().HasFilter("NOT \"IsDeleted\"");
        b.Entity<Collection>().HasIndex(x => new { x.IsDeleted, x.IsActive, x.Type });
        b.Entity<Collection>().Property(x => x.RulesJson).HasColumnType("jsonb");

        b.Entity<CollectionProduct>().HasKey(x => new { x.CollectionId, x.ProductId });
        b.Entity<CollectionProduct>().HasIndex(x => new { x.CollectionId, x.SortOrder });
        b.Entity<CollectionProduct>()
            .HasOne(x => x.Collection)
            .WithMany(c => c.Items)
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CollectionProduct>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CollectionPage>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<CollectionPage>().HasIndex(x => new { x.IsPublished, x.CollectionId });
        b.Entity<CollectionPage>()
            .HasOne(x => x.Collection)
            .WithMany(c => c.Pages)
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
