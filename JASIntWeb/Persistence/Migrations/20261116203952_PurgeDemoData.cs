using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261116203952_PurgeDemoData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Products') THEN
                    TRUNCATE TABLE 
                        ""OrderTimelineEvents"",
                        ""Payments"",
                        ""OrderItems"",
                        ""Orders"",
                        ""CartItems"",
                        ""Carts"",
                        ""CartMergeReceipts"",
                        ""ProductReviews"",
                        ""InventoryTransactions"",
                        ""InventoryItems"",
                        ""ProductVariantOptions"",
                        ""ProductVariants"",
                        ""ProductImages"",
                        ""ProductAttributeOptions"",
                        ""ProductAttributeDefinitions"",
                        ""CategoryPlacements"",
                        ""Products"",
                        ""AttributeOptions"",
                        ""AttributeDefinitions"",
                        ""Brands"",
                        ""Categories"",
                        ""CarouselSlides"",
                        ""HomepageSections"",
                        ""CuratedPromoCards"",
                        ""Coupons"",
                        ""DeliveryAddresses"",
                        ""UrlRedirects"",
                        ""Guides"",
                        ""VisitorLogs""
                    CASCADE;

                    DELETE FROM ""AspNetUsers"" WHERE ""Id"" NOT IN (SELECT ""UserId"" FROM ""AspNetUserRoles"");
                END IF;
            END $$;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
