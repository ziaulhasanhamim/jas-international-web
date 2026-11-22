using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
using System.Text.Json;
namespace JASIntWeb.Tests.Unit;

public class ProductEditorTests
{
    [Test]
    public async Task MixedOptionAndVariantChangesRemainSaveableAndValidationRetainsDraft()
    {
        var state = new ProductEditorSession();
        state.Load(new(new(), [], [], [], "https://example.test"));
        state.Change(d => d.Attributes.Add(new() { DefinitionId = 1, Name = "Color" }));
        state.Change(d => d.Variants.Add(new() { Price = 123.45m }));
        await state.Busy.Should().BeFalse();
        state.Phase = EditorPhase.Saving;
        state.Reject(new("Correct the label", 422, new() { ["Options"] = ["Missing label"] }));
        await state.Busy.Should().BeFalse();
        await state.Draft.Variants.Single().Price.Should().BeEqualTo(123.45m);
        await state.Draft.Attributes.Single().Name.Should().BeEqualTo("Color");
        await state.Dirty.Should().BeTrue();
        state.Reject(new("Sign in", 401)); await state.Busy.Should().BeFalse();
        await state.Phase.Should().BeEqualTo(EditorPhase.SessionExpired);
        state.Reject(new("Stale", 409)); await state.Phase.Should().BeEqualTo(EditorPhase.Conflict);
    }
    [Test]
    public async Task MetadataHasNativeTypesAndOnlyConfiguredColorIsPublic()
    {
        List<ValueField> fields = [new("finish", "Finish", ValueFieldKind.Text), new("weight", "Weight", ValueFieldKind.Number), new("wireless", "Wireless", ValueFieldKind.Boolean), new("color", "Color", ValueFieldKind.HexColor)];
        var json = ProductEditorRules.EncodeMetadata(fields, new Dictionary<string, string> { ["finish"] = "Matte", ["weight"] = "2.5", ["wireless"] = "true", ["color"] = "#ffffff" });
        using var parsed = JsonDocument.Parse(json);
        await parsed.RootElement.GetProperty("weight").GetDecimal().Should().BeEqualTo(2.5m);
        await parsed.RootElement.GetProperty("wireless").GetBoolean().Should().BeTrue();
        await ProductEditorRules.Swatch(json, "color").Should().BeEqualTo("#FFFFFF");
        await ProductEditorRules.Swatch(json, "finish").Should().BeNull();
        await ProductEditorRules.Swatch(json, null).Should().BeNull();
    }
    [Test]
    public async Task InvalidMetadataAndSchemaAreRejected()
    {
        foreach (var input in new[] { "red", "#fff", "#FFFFFF;background:red", "#GG0000" })
        {
            var rejected = false; try { ProductEditorRules.EncodeMetadata([new("color", "Color", ValueFieldKind.HexColor)], new Dictionary<string, string> { ["color"] = input }); } catch (ArgumentException) { rejected = true; }
            await rejected.Should().BeTrue();
        }
        var invalid = false; try { ProductEditorRules.ValidateFields([new("Bad key", "Bad", ValueFieldKind.Text)], null); } catch (ArgumentException) { invalid = true; }
        await invalid.Should().BeTrue();
    }
    [Test]
    public async Task AutomaticSkuIsReadableBoundedAndUsesStableClientIdentity()
    {
        var option = new EditorOption { Value = "Midnight Black" };
        var draft = new ProductEditorDraft { Information = new() { Name = "Realme Buds Wireless 5 ANC" }, Attributes = [new() { DefinitionId = 1, Options = [option] }] };
        var variant = new EditorVariant { ClientId = Guid.Parse("12345678-0000-0000-0000-000000000001"), Choices = new() { [1] = option.ClientId } };
        await ProductEditorRules.AutoSku(draft, variant).Should().BeEqualTo("JAS-REA-BW5A-MBLK-1234");
        variant.Choices.Clear(); await ProductEditorRules.AutoSku(draft, variant).Contains("-BASE-").Should().BeTrue();
        draft.Information.Name = new('x', 200); await (ProductEditorRules.AutoSku(draft, variant).Length <= 35).Should().BeTrue();
    }
    [Test]
    public async Task StagingSelectsNothingImplicitlyAndDoesNotDuplicateOrRestoreRows()
    {
        var state = new ProductEditorSession(); state.Load(new(new(), [], [], [], ""));
        var option = Guid.NewGuid(); var choices = new Dictionary<int, Guid> { [1] = option }; var key = ProductEditorRules.ChoiceKey(choices);
        state.Stage([]); await state.Draft.Variants.Count.Should().BeEqualTo(0);
        state.Stage([new(key, choices, "Black", "Archived", [1])]); await state.Draft.Variants.Count.Should().BeEqualTo(0);
        state.Stage([new(key, choices, "Black", "Missing", [])]);
        state.Stage([new(key, choices, "Black", "Missing", [])]); await state.Draft.Variants.Count.Should().BeEqualTo(1);
        await state.Draft.Variants[0].OpeningStock.Should().BeEqualTo(0);
    }
}
