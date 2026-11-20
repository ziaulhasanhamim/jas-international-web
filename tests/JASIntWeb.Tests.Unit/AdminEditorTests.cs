using JASIntWeb.Client.Services;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Components;
namespace JASIntWeb.Tests.Unit;

public class AdminEditorTests
{
    [Test]
    [Arguments("https://media.example.test/image.webp", true)]
    [Arguments("/uploads/category.png", true)]
    [Arguments("⌚", false)]
    [Arguments("javascript:alert(1)", false)]
    [Arguments("//untrusted.example/icon.png", false)]
    public async Task CategoryVisual_RecognizesImageLocationsWithoutTreatingEmojiOrScriptsAsImages(string value, bool expected)
        => await CategoryVisuals.IsImage(value).Should().BeEqualTo(expected);

    [Test]
    public async Task SeoPreview_UsesSameTitleAndDescriptionRulesAsStorefront()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var service = new JASIntWeb.Services.Seo.SeoService(config);
        await AdminSeoText.BuildCategoryTitle("Smart Wearables").Should().BeEqualTo(service.BuildCategoryTitle("Smart Wearables"));
        await AdminSeoText.BuildBrandTitle("Realme").Should().BeEqualTo(service.BuildBrandTitle("Realme"));
        await AdminSeoText.BuildProductTitle("Buds", "Realme").Should().BeEqualTo(service.BuildProductTitle("Buds", "Realme"));
        await AdminSeoText.SanitizeDescription("<p>Useful &amp; genuine gadgets</p>").Should().BeEqualTo("Useful & genuine gadgets");
        await AdminSeoText.SanitizeDescription(string.Join(" ", Enumerable.Repeat("gadgets", 40))).EndsWith("…").Should().BeTrue();
    }

    [Test]
    public async Task Slugs_NormalizeDiacritics()
        => await EditorSlug.FromName("Café Smart Wearables!").Should().BeEqualTo("cafe-smart-wearables");

    [Test]
    [Arguments("category", 61, 156, true)]
    [Arguments("category", 100, 300, true)]
    [Arguments("category", 101, 300, false)]
    [Arguments("category", 100, 301, false)]
    [Arguments("brand", 61, 156, true)]
    [Arguments("brand", 100, 300, true)]
    [Arguments("brand", 101, 300, false)]
    [Arguments("brand", 100, 301, false)]
    [Arguments("product", 61, 156, true)]
    [Arguments("product", 100, 300, true)]
    [Arguments("product", 101, 300, false)]
    [Arguments("product", 100, 301, false)]
    public async Task EditorValidation_GuidelinesDoNotReplaceStorageLimits(string kind, int titleLength, int descriptionLength, bool valid)
    {
        object input = kind switch
        {
            "category" => new JASIntWeb.Client.Dtos.CategoryEditForm { Name = "Category", MetaTitle = new string('t', titleLength), MetaDescription = new string('d', descriptionLength) },
            "brand" => new JASIntWeb.Client.Dtos.BrandEditForm { Name = "Brand", MetaTitle = new string('t', titleLength), MetaDescription = new string('d', descriptionLength) },
            "product" => new JASIntWeb.Client.Dtos.ProductEditForm { Name = "Product", CategoryId = 1, Details = "Details", Image = "/image.png", MetaTitle = new string('t', titleLength), MetaDescription = new string('d', descriptionLength) },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        List<System.ComponentModel.DataAnnotations.ValidationResult> errors = [];
        var accepted = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input, new(input), errors, true);
        await accepted.Should().BeEqualTo(valid);
        if (!valid) await errors.Any(error => error.MemberNames.Any(name => name is "MetaTitle" or "MetaDescription")).Should().BeTrue();
    }

    private sealed class Navigation : NavigationManager { public Navigation(string uri) => Initialize("https://example.test/", uri); }
    private sealed record Row(int Id, string Name);
    [Test]
    public async Task AdminSorting_WhitelistsColumnsAndSortsBeforePaginationWithStableTies()
    {
        var rows = new[] { new Row(3, "A"), new Row(2, "Z"), new Row(1, "A") }.AsQueryable();
        await AdminSorting.Apply(rows, new Navigation("https://example.test/admin"), ("name", r => r.Name)).First().Id.Should().BeEqualTo(3);
        var sorted = AdminSorting.Apply(rows, new Navigation("https://example.test/admin?sort=name&direction=asc"), ("name", r => r.Name)).Skip(1).Take(1).Single();
        await sorted.Id.Should().BeEqualTo(3);
        var unknown = AdminSorting.Apply(rows, new Navigation("https://example.test/admin?sort=PasswordHash&direction=desc"), ("name", r => r.Name)).First();
        await unknown.Id.Should().BeEqualTo(3);
        var descending = AdminSorting.Apply(rows, new Navigation("https://example.test/admin?sort=name&direction=desc"), ("name", r => r.Name)).First();
        await descending.Id.Should().BeEqualTo(2);
    }

    [Test]
    public async Task AdminDraftSerialization_PreservesFormsAcrossNavigation()
    {
        var brandDraft = new JASIntWeb.Client.Dtos.BrandEditForm
        {
            Name = "Draft Brand",
            Slug = "draft-brand",
            Description = "Unsaved draft",
            IsFeatured = true,
            IntroText = "<p>Draft intro</p>"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(brandDraft);
        var restored = System.Text.Json.JsonSerializer.Deserialize<JASIntWeb.Client.Dtos.BrandEditForm>(json);

        await restored.Should().NotBeNull();
        await restored!.Name.Should().BeEqualTo("Draft Brand");
        await restored.Slug.Should().BeEqualTo("draft-brand");
        await restored.IsFeatured.Should().BeTrue();
        await restored.IntroText.Should().BeEqualTo("<p>Draft intro</p>");
    }

    [Test]
    public async Task ProductEditorDraft_SerializesAndRestoresCorrectly()
    {
        var draft = new JASIntWeb.Client.Contracts.ProductEditorDraft
        {
            Id = 0,
            Information = new()
            {
                Name = "Unsaved Realme Earbuds",
                Slug = "unsaved-realme-earbuds",
                CategoryId = 2,
                Details = "<p>Rich details draft</p>",
                Hidden = true
            },
            Variants =
            [
                new()
                {
                    ClientId = Guid.NewGuid(),
                    Sku = "JAS-REA-EAR-BLK-1234",
                    Price = 2499.00m,
                    Active = true,
                    Default = true
                }
            ]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(draft);
        var restored = System.Text.Json.JsonSerializer.Deserialize<JASIntWeb.Client.Contracts.ProductEditorDraft>(json);

        await restored.Should().NotBeNull();
        await restored!.Information.Name.Should().BeEqualTo("Unsaved Realme Earbuds");
        await restored.Information.CategoryId.Should().BeEqualTo(2);
        await restored.Variants.Count.Should().BeEqualTo(1);
        await restored.Variants[0].Price.Should().BeEqualTo(2499.00m);
        await restored.Variants[0].Sku.Should().BeEqualTo("JAS-REA-EAR-BLK-1234");
    }
}
