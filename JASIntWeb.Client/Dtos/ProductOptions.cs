namespace JASIntWeb.Client.Dtos;
// Shared pure selection rules. The SSR page keeps choices even when no variant matches.

public static class ProductOptions
{
    public static Dictionary<string, string> Selection(ProductDetailsDto product, IReadOnlyDictionary<string, string> requested)
    {
        var initial = product.Variants.FirstOrDefault(v => v.IsDefault) ?? product.Variants.FirstOrDefault();
        var choices = initial is null ? new Dictionary<string, string>() : new Dictionary<string, string>(initial.Options);
        foreach (var attribute in product.Attributes)
        {
            if (!requested.TryGetValue(attribute.Code, out var value))
            {
                continue;
            }
            if (value.Length == 0)
            {
                choices.Remove(attribute.Code);
            }
            else
            {
                choices[attribute.Code] = value;
            }
        }
        return choices;
    }

    public static ProductVariantDto? Match(ProductDetailsDto product, IReadOnlyDictionary<string, string> choices) =>
    product.Variants.FirstOrDefault(v => v.Options.Count == choices.Count
        &&
        choices.All(a => v.Options.TryGetValue(a.Key, out var value) && value == a.Value));

    public static string OptionUrl(ProductDetailsDto product, IReadOnlyDictionary<string, string> choices, string code, string value)
    {
        var next = new Dictionary<string, string>(choices);
        if (value.Length == 0)
        {
            next.Remove(code);
        }
        else
        {
            next[code] = value;
        }
        // Include empty values so an intentionally omitted optional attribute stays omitted.
        var query = string.Join("&", product.Attributes.Select(a =>
                Uri.EscapeDataString("attribute-" + a.Code) + "=" + Uri.EscapeDataString(next.GetValueOrDefault(a.Code) ?? "")));
        return "/product/" + Uri.EscapeDataString(product.Slug) + (query.Length == 0 ? "" : "?" + query);
    }
}
