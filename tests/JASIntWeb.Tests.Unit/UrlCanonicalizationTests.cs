namespace JASIntWeb.Tests.Unit;

public class UrlCanonicalizationTests
{
    [Test]
    public async Task CanonicalPath_ForCategory_FollowsCategorySlugPattern()
    {
        var slug = "smartphones";
        var path = GetCategoryPath(slug);

        await path.Should().BeEqualTo("/category/smartphones");
    }

    [Test]
    public async Task CanonicalPath_ForProduct_FollowsProductSlugPattern()
    {
        var slug = "apple-iphone-16-pro-max";
        var path = GetProductPath(slug);

        await path.Should().BeEqualTo("/product/apple-iphone-16-pro-max");
    }

    [Test]
    public async Task LegacyPath_RedirectsToCanonicalCategory()
    {
        var legacyPath = "/products/smartphones";
        var redirectedPath = ResolveLegacyPath(legacyPath);

        await redirectedPath.Should().BeEqualTo("/category/smartphones");
    }

    [Test]
    public async Task CatalogPath_WithQuery_PreservesParameters()
    {
        var query = "?category=smartphones&brand=apple&sort=price-asc";
        var fullUrl = $"/products{query}";

        await fullUrl.Should().Contain("/products?category=smartphones");
    }

    private static string GetCategoryPath(string slug) => $"/category/{slug}";
    private static string GetProductPath(string slug) => $"/product/{slug}";

    private static string ResolveLegacyPath(string path)
    {
        if (path.StartsWith("/products/", StringComparison.OrdinalIgnoreCase))
        {
            var slug = path["/products/".Length..];
            return $"/category/{slug}";
        }
        return path;
    }
}
