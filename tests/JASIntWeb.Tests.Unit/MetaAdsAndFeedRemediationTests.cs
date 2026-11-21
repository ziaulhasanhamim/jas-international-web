using JASIntWeb.Endpoints;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Tests.Unit;

public class MetaAdsAndFeedRemediationTests
{
    private static Product CreateDummyProduct(string name) => new()
    {
        Name = name,
        Slug = "test-slug",
        Details = "<p>Details</p>",
        MainImageUrl = "/test.webp",
        CategoryId = 1
    };

    [Test]
    [Arguments("true-wireless-earbuds", "Electronics > Audio > Audio Components > Headphones & Headsets")]
    [Arguments("neckband", "Electronics > Audio > Audio Components > Headphones & Headsets")]
    [Arguments("speakers", "Electronics > Audio > Audio Components > Speakers")]
    [Arguments("smart-watches", "Electronics > Wearable Technology > Smartwatches")]
    [Arguments("power-banks", "Electronics > Electronics Accessories > Power > Power Adapters & Chargers")]
    [Arguments("cables", "Electronics > Electronics Accessories > Cables")]
    [Arguments("tripods-lighting", "Cameras & Optics > Camera & Optic Accessories > Tripods & Monopods")]
    [Arguments("cases-protection", "Electronics > Electronics Accessories")]
    [Arguments("unknown-category", "Electronics")]
    [Arguments(null, "Electronics")]
    public async Task ResolveGoogleProductCategory_MapsAllStorefrontTaxonomiesAccurately(string? categorySlug, string expectedTaxonomy)
    {
        var result = MetaFeedEndpoints.ResolveGoogleProductCategory(categorySlug);
        await result.Should().BeEqualTo(expectedTaxonomy);
    }

    [Test]
    public async Task BuildVariantTitle_FormatsOptionsAccurately()
    {
        var product = CreateDummyProduct("realme Buds Wireless 5 ANC Neckband");
        var defColor = new ProductAttributeDefinition { SortOrder = 1 };
        var defWarranty = new ProductAttributeDefinition { SortOrder = 2 };

        var optColor = new ProductAttributeOption { Value = "Black", ProductAttributeDefinition = defColor };
        var optWarranty = new ProductAttributeOption { Value = "12 Months Warranty", ProductAttributeDefinition = defWarranty };

        var variant = new ProductVariant
        {
            Product = product,
            SKU = "REALME-BW5-BLK",
            OptionSignature = "2-3",
            Options =
            [
                new ProductVariantOption { ProductAttributeOption = optColor },
                new ProductVariantOption { ProductAttributeOption = optWarranty }
            ]
        };

        var title = MetaFeedEndpoints.BuildVariantTitle(variant, product);
        await title.Should().BeEqualTo("realme Buds Wireless 5 ANC Neckband - Black / 12 Months Warranty");
    }

    [Test]
    public async Task BuildVariantTitle_OmitsNumericOptionSignatures()
    {
        var product = CreateDummyProduct("realme Buds Wireless 5 ANC Neckband");
        var variant = new ProductVariant
        {
            Product = product,
            SKU = "REALME-BW5-BLK",
            OptionSignature = "2-3",
            Options = []
        };

        var title = MetaFeedEndpoints.BuildVariantTitle(variant, product);
        await title.Should().BeEqualTo("realme Buds Wireless 5 ANC Neckband");
    }

    [Test]
    public async Task BuildVariantTitle_PreservesHumanReadableOptionSignature()
    {
        var product = CreateDummyProduct("Anker 737 Power Bank");
        var variant = new ProductVariant
        {
            Product = product,
            SKU = "ANKER-737",
            OptionSignature = "24000mAh 140W",
            Options = []
        };

        var title = MetaFeedEndpoints.BuildVariantTitle(variant, product);
        await title.Should().BeEqualTo("Anker 737 Power Bank - 24000mAh 140W");
    }
}
