using System.Globalization;
using System.Text.Json;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Services;

public sealed record CollectionProductCandidate(
    int Id,
    string Name,
    string Slug,
    int CategoryId,
    int? BrandId,
    string? BrandSlug,
    IReadOnlyCollection<int> TagIds,
    decimal Price,
    decimal? CompareAtPrice,
    int AvailableStock,
    bool IsFeatured,
    bool IsNewArrival,
    decimal Rating,
    DateTimeOffset CreatedAt
);

public sealed record CollectionRuleContext(
    IReadOnlyDictionary<int, HashSet<int>> CategorySubtrees,
    IReadOnlyDictionary<string, int> CategorySlugToId,
    IReadOnlyDictionary<string, int> TagSlugToId,
    IReadOnlyDictionary<string, int> BrandSlugToId
);

public static class CollectionRuleEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeRules(CollectionRuleGroup group) =>
        JsonSerializer.Serialize(group, JsonOptions);

    public static CollectionRuleGroup DeserializeRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CollectionRuleGroup();
        }

        try
        {
            return JsonSerializer.Deserialize<CollectionRuleGroup>(json, JsonOptions) ?? new CollectionRuleGroup();
        }
        catch
        {
            return new CollectionRuleGroup();
        }
    }

    public static bool HasAnyRules(CollectionRuleGroup? group)
    {
        if (group is null) return false;
        if (group.Rules.Count > 0) return true;
        return group.Groups.Any(HasAnyRules);
    }

    public static IReadOnlyList<CollectionProductCandidate> EvaluateAndSort(
        IEnumerable<CollectionProductCandidate> candidates,
        CollectionRuleGroup? rootGroup,
        CollectionSortOrder sortMode,
        CollectionRuleContext ctx)
    {
        if (!HasAnyRules(rootGroup))
        {
            return [];
        }

        var matched = candidates.Where(c => MatchesGroup(c, rootGroup!, ctx));
        return SortCandidates(matched, sortMode).ToList();
    }

    public static IEnumerable<CollectionProductCandidate> SortCandidates(
        IEnumerable<CollectionProductCandidate> items,
        CollectionSortOrder sortMode) => sortMode switch
        {
            CollectionSortOrder.PriceLowToHigh => items.OrderBy(p => p.Price).ThenBy(p => p.Id),
            CollectionSortOrder.PriceHighToLow => items.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            CollectionSortOrder.NameAsc => items.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id),
            CollectionSortOrder.BestRated => items.OrderByDescending(p => p.Rating).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            CollectionSortOrder.FeaturedFirst => items.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            CollectionSortOrder.BiggestDiscount => items.OrderByDescending(GetDiscountPercent).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            _ => items.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };

    public static bool MatchesGroup(
        CollectionProductCandidate product,
        CollectionRuleGroup group,
        CollectionRuleContext ctx)
    {
        var nonEmptySubgroups = group.Groups.Where(HasAnyRules).ToList();
        if (group.Rules.Count == 0 && nonEmptySubgroups.Count == 0)
        {
            return true;
        }

        bool isOr = group.MatchOperator == CollectionRuleLogicalOp.Or;

        if (isOr)
        {
            foreach (var rule in group.Rules)
            {
                if (MatchesRule(product, rule, ctx)) return true;
            }
            foreach (var sub in nonEmptySubgroups)
            {
                if (MatchesGroup(product, sub, ctx)) return true;
            }
            return false;
        }
        else
        {
            foreach (var rule in group.Rules)
            {
                if (!MatchesRule(product, rule, ctx)) return false;
            }
            foreach (var sub in nonEmptySubgroups)
            {
                if (!MatchesGroup(product, sub, ctx)) return false;
            }
            return true;
        }
    }

    public static bool MatchesRule(
        CollectionProductCandidate product,
        CollectionRuleCondition rule,
        CollectionRuleContext ctx)
    {
        return rule.Field switch
        {
            CollectionRuleField.Tag => EvaluateTag(product, rule, ctx),
            CollectionRuleField.Category => EvaluateCategory(product, rule, ctx),
            CollectionRuleField.Brand => EvaluateBrand(product, rule, ctx),
            CollectionRuleField.Price => CompareDecimal(product.Price, rule.Operator, rule.Value),
            CollectionRuleField.Stock => CompareDecimal(product.AvailableStock, rule.Operator, rule.Value),
            CollectionRuleField.DiscountPercent => CompareDecimal(GetDiscountPercent(product), rule.Operator, rule.Value),
            CollectionRuleField.HasDiscount => EvaluateBool(
                product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.Price,
                rule.Operator,
                rule.Value),
            CollectionRuleField.IsFeatured => EvaluateBool(product.IsFeatured, rule.Operator, rule.Value),
            CollectionRuleField.IsNewArrival => EvaluateBool(product.IsNewArrival, rule.Operator, rule.Value),
            CollectionRuleField.Title => EvaluateText(product.Name, rule.Operator, rule.Value),
            _ => false
        };
    }

    private static decimal GetDiscountPercent(CollectionProductCandidate product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= product.Price || product.CompareAtPrice.Value <= 0)
        {
            return 0m;
        }

        return Math.Round((product.CompareAtPrice.Value - product.Price) / product.CompareAtPrice.Value * 100m, 2);
    }

    private static bool EvaluateTag(CollectionProductCandidate product, CollectionRuleCondition rule, CollectionRuleContext ctx)
    {
        var targetId = ResolveId(rule.Value, ctx.TagSlugToId);
        bool hasTag = targetId.HasValue && product.TagIds.Contains(targetId.Value);
        return rule.Operator == CollectionRuleOperator.NotEquals ? !hasTag : hasTag;
    }

    private static bool EvaluateCategory(CollectionProductCandidate product, CollectionRuleCondition rule, CollectionRuleContext ctx)
    {
        var targetId = ResolveId(rule.Value, ctx.CategorySlugToId);
        if (!targetId.HasValue)
        {
            return rule.Operator == CollectionRuleOperator.NotEquals;
        }

        bool inCategory = ctx.CategorySubtrees.TryGetValue(targetId.Value, out var subtree)
            ? subtree.Contains(product.CategoryId)
            : product.CategoryId == targetId.Value;

        return rule.Operator == CollectionRuleOperator.NotEquals ? !inCategory : inCategory;
    }

    private static bool EvaluateBrand(CollectionProductCandidate product, CollectionRuleCondition rule, CollectionRuleContext ctx)
    {
        var targetId = ResolveId(rule.Value, ctx.BrandSlugToId);
        bool matches = targetId.HasValue && product.BrandId.HasValue && product.BrandId.Value == targetId.Value;
        return rule.Operator == CollectionRuleOperator.NotEquals ? !matches : matches;
    }

    private static int? ResolveId(string? value, IReadOnlyDictionary<string, int> slugMap)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }

        var slug = trimmed.ToLowerInvariant();
        return slugMap.TryGetValue(slug, out var mappedId) ? mappedId : null;
    }

    private static bool CompareDecimal(decimal actual, CollectionRuleOperator op, string? rawTarget)
    {
        if (string.IsNullOrWhiteSpace(rawTarget) ||
            !decimal.TryParse(rawTarget.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var target))
        {
            return false;
        }

        return op switch
        {
            CollectionRuleOperator.Equals => actual == target,
            CollectionRuleOperator.NotEquals => actual != target,
            CollectionRuleOperator.GreaterThan => actual > target,
            CollectionRuleOperator.GreaterThanOrEqual => actual >= target,
            CollectionRuleOperator.LessThan => actual < target,
            CollectionRuleOperator.LessThanOrEqual => actual <= target,
            _ => false
        };
    }

    private static bool EvaluateBool(bool actual, CollectionRuleOperator op, string? rawTarget)
    {
        if (op == CollectionRuleOperator.IsTrue) return actual;
        if (op == CollectionRuleOperator.IsFalse) return !actual;

        if (!bool.TryParse(rawTarget?.Trim(), out var expected))
        {
            expected = true;
        }

        return op == CollectionRuleOperator.NotEquals ? actual != expected : actual == expected;
    }

    private static bool EvaluateText(string actual, CollectionRuleOperator op, string? rawTarget)
    {
        var target = rawTarget?.Trim() ?? "";
        if (string.IsNullOrEmpty(target)) return false;

        return op switch
        {
            CollectionRuleOperator.Equals => actual.Equals(target, StringComparison.OrdinalIgnoreCase),
            CollectionRuleOperator.NotEquals => !actual.Equals(target, StringComparison.OrdinalIgnoreCase),
            CollectionRuleOperator.Contains => actual.Contains(target, StringComparison.OrdinalIgnoreCase),
            CollectionRuleOperator.NotContains => !actual.Contains(target, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
