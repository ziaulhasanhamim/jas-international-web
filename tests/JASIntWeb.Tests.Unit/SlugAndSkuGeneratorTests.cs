using JASIntWeb.Services;

namespace JASIntWeb.Tests.Unit;

public class SlugAndSkuGeneratorTests
{
    [Test]
    public async Task GenerateSlug_WithStandardTitle_CreatesCleanHyphenatedSlug()
    {
        var slug = SlugAndSkuGenerator.GenerateSlug("Apple iPhone 16 Pro Max");
        await slug.Should().BeEqualTo("apple-iphone-16-pro-max");
    }

    [Test]
    public async Task GenerateSlug_WithSymbolsAndSpecialCharacters_CleansPunctuation()
    {
        var slug = SlugAndSkuGenerator.GenerateSlug("Marshall Kilburn III (Portable & Bluetooth!) + Free Cable @ 100%");
        await slug.Should().BeEqualTo("marshall-kilburn-iii-portable-and-bluetooth-plus-free-cable-at-100-percent");
    }

    [Test]
    public async Task GenerateSlug_WithWhitespaceAndExtraHyphens_NormalizesToSingleHyphen()
    {
        var slug = SlugAndSkuGenerator.GenerateSlug("   Sony   WH-1000XM5---Wireless   Noise   Cancelling   ");
        await slug.Should().BeEqualTo("sony-wh-1000xm5-wireless-noise-cancelling");
    }

    [Test]
    public async Task GenerateSlug_WhenNullOrEmpty_ReturnsFallbackPrefixWithRandomNumber()
    {
        var slug = SlugAndSkuGenerator.GenerateSlug("", "gadget");
        await slug.StartsWith("gadget-").Should().BeTrue();
    }

    [Test]
    public async Task GenerateSku_WithBrandAndProduct_GeneratesStandardSku()
    {
        var sku = SlugAndSkuGenerator.GenerateSku("iPhone 16 Pro Max", "Apple");
        await sku.StartsWith("APPL-").Should().BeTrue();
        await sku.Contains("16").Should().BeTrue();
    }

    [Test]
    public async Task GenerateSku_WithVariantOptions_AppendsOptionCodes()
    {
        var sku = SlugAndSkuGenerator.GenerateSku("iPhone 16 Pro", "Apple", ["Desert Titanium", "256GB"]);
        await sku.Should().BeEqualTo("APPL-IPH16PRO-DES-256GB");
    }

    [Test]
    public async Task GenerateEan8Barcode_WithValidProductId_GeneratesValidEan8()
    {
        // ID 1 should generate 7 digits "0000001" + check digit 7 => "00000017"
        var barcode = SlugAndSkuGenerator.GenerateEan8Barcode(1);
        await barcode.Should().BeEqualTo("00000017");
        await SlugAndSkuGenerator.IsValidEan8(barcode).Should().BeTrue();
    }

    [Test]
    public async Task GenerateEan8Barcode_WithArbitraryIds_AlwaysPassesValidation()
    {
        int[] testIds = [42, 999, 12345, 987654];
        foreach (var id in testIds)
        {
            var code = SlugAndSkuGenerator.GenerateEan8Barcode(id);
            await code.Length.Should().BeEqualTo(8);
            await SlugAndSkuGenerator.IsValidEan8(code).Should().BeTrue();
        }
    }

    [Test]
    public async Task IsValidEan8_WithInvalidChecksum_ReturnsFalse()
    {
        // 00000017 is valid; 00000018 has wrong checksum digit
        await SlugAndSkuGenerator.IsValidEan8("00000018").Should().BeFalse();
        await SlugAndSkuGenerator.IsValidEan8("abc12345").Should().BeFalse();
        await SlugAndSkuGenerator.IsValidEan8("").Should().BeFalse();
    }
}
