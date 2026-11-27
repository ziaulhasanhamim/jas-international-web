using JASIntWeb.Client.Dtos;
using JASIntWeb.Persistence.Entities;
using JASIntWeb.Services;

namespace JASIntWeb.Tests.Unit;

public class CollectionRuleEvaluatorTests
{
    private static CollectionRuleContext BuildContext()
    {
        // Parent category 1 (Audio) -> child category 2 (Microphone)
        var subtrees = new Dictionary<int, HashSet<int>>
        {
            [1] = [1, 2],
            [2] = [2],
            [3] = [3]
        };

        var catSlugs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio"] = 1,
            ["microphone"] = 2,
            ["smartwatch"] = 3
        };

        var tagSlugs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["eid-sale"] = 10,
            ["clearance"] = 20
        };

        var brandSlugs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["boya"] = 100,
            ["ulanzi"] = 200
        };

        return new CollectionRuleContext(subtrees, catSlugs, tagSlugs, brandSlugs);
    }

    [Test]
    public async Task EvaluateAndSort_ComplexNestedOrAndStockPriceRule_MatchesExactProducts()
    {
        var ctx = BuildContext();
        var now = DateTimeOffset.UtcNow;

        // (Tag = eid-sale OR Category = microphone) AND Stock > 0 AND Price < 3000
        var rules = new CollectionRuleGroup
        {
            MatchOperator = CollectionRuleLogicalOp.And,
            Groups =
            [
                new CollectionRuleGroup
                {
                    MatchOperator = CollectionRuleLogicalOp.Or,
                    Rules =
                    [
                        new CollectionRuleCondition { Field = CollectionRuleField.Tag, Operator = CollectionRuleOperator.Equals, Value = "eid-sale" },
                        new CollectionRuleCondition { Field = CollectionRuleField.Category, Operator = CollectionRuleOperator.Equals, Value = "microphone" }
                    ]
                }
            ],
            Rules =
            [
                new CollectionRuleCondition { Field = CollectionRuleField.Stock, Operator = CollectionRuleOperator.GreaterThan, Value = "0" },
                new CollectionRuleCondition { Field = CollectionRuleField.Price, Operator = CollectionRuleOperator.LessThan, Value = "3000" }
            ]
        };

        var candidates = new List<CollectionProductCandidate>
        {
            // Match 1: eid-sale tag + smartwatch cat + stock 5 + price 2400
            new(1, "Smartwatch Eid Edition", "watch-eid", 3, 100, "boya", [10], 2400m, 3000m, 5, true, false, 4.9m, now.AddDays(-2)),
            // Match 2: microphone cat (no tag) + stock 2 + price 1500
            new(2, "Boya BY-M1 Mic", "boya-mic", 2, 100, "boya", [], 1500m, null, 2, false, true, 4.8m, now.AddDays(-1)),
            // Fail: Out of stock (stock == 0)
            new(3, "Out of Stock Mic", "oos-mic", 2, 100, "boya", [10], 1200m, null, 0, false, false, 4.5m, now),
            // Fail: Price == 3000 (not strictly < 3000)
            new(4, "Expensive Mic", "exp-mic", 2, 100, "boya", [10], 3000m, null, 10, false, false, 5.0m, now),
            // Fail: Neither eid-sale nor microphone
            new(5, "Unrelated Watch", "watch-reg", 3, 100, "boya", [20], 900m, null, 10, false, false, 4.0m, now)
        };

        var matched = CollectionRuleEvaluator.EvaluateAndSort(candidates, rules, CollectionSortOrder.PriceLowToHigh, ctx);

        await matched.Count.Should().BeEqualTo(2);
        await matched[0].Id.Should().BeEqualTo(2); // 1500 first
        await matched[1].Id.Should().BeEqualTo(1); // 2400 second
    }

    [Test]
    public async Task EvaluateAndSort_CategoryParentMatchAndBrandExclusion_WorksTogether()
    {
        var ctx = BuildContext();
        var now = DateTimeOffset.UtcNow;

        // Category == audio (includes child category 2 Microphone) AND Brand != ulanzi
        var rules = new CollectionRuleGroup
        {
            MatchOperator = CollectionRuleLogicalOp.And,
            Rules =
            [
                new CollectionRuleCondition { Field = CollectionRuleField.Category, Operator = CollectionRuleOperator.Equals, Value = "1" },
                new CollectionRuleCondition { Field = CollectionRuleField.Brand, Operator = CollectionRuleOperator.NotEquals, Value = "ulanzi" }
            ]
        };

        var candidates = new List<CollectionProductCandidate>
        {
            new(10, "Boya Studio Mic", "boya-studio", 2, 100, "boya", [], 2500m, null, 4, false, false, 4.7m, now),
            new(11, "Ulanzi Pocket Mic", "ulanzi-mic", 2, 200, "ulanzi", [], 2200m, null, 4, false, false, 4.6m, now)
        };

        var matched = CollectionRuleEvaluator.EvaluateAndSort(candidates, rules, CollectionSortOrder.Newest, ctx);
        await matched.Count.Should().BeEqualTo(1);
        await matched[0].Id.Should().BeEqualTo(10);
    }

    [Test]
    public async Task EvaluateAndSort_EmptyRules_ReturnsEmpty()
    {
        var ctx = BuildContext();
        var now = DateTimeOffset.UtcNow;
        var candidates = new List<CollectionProductCandidate>
        {
            new(1, "Item", "item", 1, 100, "boya", [], 1000m, null, 5, false, false, 5m, now)
        };

        var matched = CollectionRuleEvaluator.EvaluateAndSort(candidates, new CollectionRuleGroup(), CollectionSortOrder.Newest, ctx);
        await matched.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task RuleSerialization_RoundTripsComplexNestedGroups()
    {
        var original = new CollectionRuleGroup
        {
            MatchOperator = CollectionRuleLogicalOp.And,
            Groups =
            [
                new CollectionRuleGroup
                {
                    MatchOperator = CollectionRuleLogicalOp.Or,
                    Rules =
                    [
                        new CollectionRuleCondition { Field = CollectionRuleField.Tag, Operator = CollectionRuleOperator.Equals, Value = "eid-sale" },
                        new CollectionRuleCondition { Field = CollectionRuleField.Category, Operator = CollectionRuleOperator.Equals, Value = "microphone" }
                    ]
                }
            ],
            Rules =
            [
                new CollectionRuleCondition { Field = CollectionRuleField.Stock, Operator = CollectionRuleOperator.GreaterThan, Value = "0" },
                new CollectionRuleCondition { Field = CollectionRuleField.Price, Operator = CollectionRuleOperator.LessThan, Value = "3000" }
            ]
        };

        var json = CollectionRuleEvaluator.SerializeRules(original);
        var restored = CollectionRuleEvaluator.DeserializeRules(json);

        await restored.MatchOperator.Should().BeEqualTo(CollectionRuleLogicalOp.And);
        await restored.Groups.Count.Should().BeEqualTo(1);
        await restored.Groups[0].MatchOperator.Should().BeEqualTo(CollectionRuleLogicalOp.Or);
        await restored.Groups[0].Rules.Count.Should().BeEqualTo(2);
        await restored.Rules.Count.Should().BeEqualTo(2);
    }
}
