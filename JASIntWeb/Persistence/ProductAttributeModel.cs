namespace JASIntWeb.Persistence;

internal static class ProductAttributeModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<ProductAttributeDefinition>(e =>
            {
                e.HasAlternateKey(x => new
                    {
                        x.Id,
                        x.ProductId,
                        x.AttributeDefinitionId
                    })
                .HasName("AK_ProductAttributeDefinitions_Ownership");
                e.HasIndex(x => new
                    {
                        x.ProductId,
                        x.AttributeDefinitionId
                    })
                .IsUnique()
                .HasDatabaseName("UX_ProductAttributes_Definition");
                e.HasIndex(x => new
                    {
                        x.ProductId,
                        x.SortOrder
                    })
                .HasDatabaseName("IX_ProductAttributes_Order");
                e.HasOne(x => x.Product)
                .WithMany(x => x.Attributes)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.AttributeDefinition)
                .WithMany()
                .HasForeignKey(x => x.AttributeDefinitionId)
                .HasConstraintName("FK_ProductAttributeDefinitions_AttributeDefinitions_AttrDefId")
                .OnDelete(DeleteBehavior.Restrict);
            });
        b.Entity<ProductAttributeOption>(e =>
            {
                e.HasAlternateKey(x => new
                    {
                        x.Id,
                        x.ProductAttributeDefinitionId,
                        x.ProductId
                    })
                .HasName("AK_ProductAttributeOptions_Ownership");
                e.HasIndex(x => new
                    {
                        x.ProductAttributeDefinitionId,
                        x.AttributeOptionId
                    })
                .IsUnique()
                .HasDatabaseName("UX_ProductAttributeOptions_Value");
                e.HasIndex(x => new
                    {
                        x.ProductAttributeDefinitionId,
                        x.SortOrder
                    })
                .HasDatabaseName("IX_ProductAttributeOptions_Order");
                e.HasOne(x => x.ProductAttributeDefinition).WithMany(x => x.Options)
                .HasForeignKey(x => new
                    {
                        x.ProductAttributeDefinitionId,
                        x.ProductId,
                        x.AttributeDefinitionId
                    })
                .HasPrincipalKey(x => new
                    {
                        x.Id,
                        x.ProductId,
                        x.AttributeDefinitionId
                    })
                .HasConstraintName("FK_ProductOptions_ProductAttribute")
                .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new
                    {
                        x.ProductAttributeDefinitionId,
                        x.ProductId,
                        x.AttributeDefinitionId
                    })
                .HasDatabaseName("IX_ProductOptions_AttributeOwner");
                e.HasOne(x => x.AttributeOption).WithMany().HasForeignKey(x => new
                    {
                        x.AttributeOptionId,
                        x.AttributeDefinitionId
                    })
                .HasPrincipalKey(x => new
                    {
                        x.Id,
                        x.AttributeDefinitionId
                    })
                .HasConstraintName("FK_ProductOptions_GlobalOption")
                .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new
                    {
                        x.AttributeOptionId,
                        x.AttributeDefinitionId
                    })
                .HasDatabaseName("IX_ProductOptions_GlobalOption");
            });
        b.Entity<ProductVariant>(e =>
            {
                e.HasAlternateKey(x => new
                    {
                        x.Id,
                        x.ProductId
                    })
                .HasName("AK_ProductVariants_Ownership");
                e.HasIndex(x => new
                    {
                        x.ProductId,
                        x.OptionSignature
                    })
                .IsUnique()
                .HasFilter("NOT \"IsDeleted\"")
                .HasDatabaseName("UX_ProductVariants_Combination");
            });
        b.Entity<ProductVariantOption>(e =>
            {
                e.HasOne(x => x.ProductVariant).WithMany(x => x.Options).HasForeignKey(x => new
                    {
                        x.ProductVariantId,
                        x.ProductId
                    })
                .HasPrincipalKey(x => new
                    {
                        x.Id,
                        x.ProductId
                    })
                .HasConstraintName("FK_VariantOptions_VariantOwner")
                .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new
                    {
                        x.ProductVariantId,
                        x.ProductId
                    })
                .HasDatabaseName("IX_VariantOptions_VariantOwner");
                e.HasOne(x => x.ProductAttributeOption).WithMany().HasForeignKey(x => new
                    {
                        x.ProductAttributeOptionId,
                        x.ProductAttributeDefinitionId,
                        x.ProductId
                    })
                .HasPrincipalKey(x => new
                    {
                        x.Id,
                        x.ProductAttributeDefinitionId,
                        x.ProductId
                    })
                .HasConstraintName("FK_VariantOptions_ProductOption")
                .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new
                    {
                        x.ProductAttributeOptionId,
                        x.ProductAttributeDefinitionId,
                        x.ProductId
                    })
                .HasDatabaseName("IX_VariantOptions_ProductOption");
                e.HasIndex(x => new
                    {
                        x.ProductVariantId,
                        x.ProductAttributeDefinitionId
                    })
                .IsUnique()
                .HasDatabaseName("UX_VariantOptions_Attribute");
            });
    }
}
