using System.Numerics;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
namespace JASIntWeb.Services.ProductEditor;

public sealed partial class ProductEditorService
{
    public async Task<EditorCombinationPage> PreviewAsync(EditorCombinationQuery query, CancellationToken ct)
    {
        var draft = query.Draft ?? throw Invalid("Options", "Provide option values before previewing.", 400);
        if (draft.Attributes is null || draft.Variants is null || draft.Variants.Any(v => v is null || v.Choices is null) || draft.Attributes.Any(a => a is null || a.Options is null || a.Options.Any(o => o is null)) || draft.Attributes.SelectMany(a => a.Options).Select(o => o.ClientId).Distinct().Count() != draft.Attributes.Sum(a => a.Options.Count) || draft.Attributes.Count > 50 || draft.Attributes.Select(a => a.DefinitionId).Distinct().Count() != draft.Attributes.Count
            || !BigInteger.TryParse(query.Offset, out var offset) || offset < 0) throw Invalid("Options", "Invalid preview filters or offset.", 400);
        List<(EditorAttribute Attribute, List<EditorOption?> Values)> axes = [];
        foreach (var attribute in draft.Attributes.Where(a => !a.Retired))
        {
            if (attribute.Options is null || attribute.Options.Count(o => !o.Retired) > 100 || attribute.Options.Any(o => string.IsNullOrWhiteSpace(o.Value))) throw Invalid("Options", "Label the option values before previewing.");
            var filter = query.Filters?.GetValueOrDefault(attribute.DefinitionId);
            var values = attribute.Options.Where(o => !o.Retired && (filter is null || filter.Contains(o.ClientId))).Cast<EditorOption?>().ToList();
            if (!attribute.Required && (filter is null || filter.Contains(Guid.Empty))) values.Add(null);
            axes.Add((attribute, values));
        }
        var total = axes.Aggregate(BigInteger.One, (count, axis) => count * axis.Values.Count);
        var existing = draft.Id > 0 ? (await LoadAsync(draft.Id, ct)).Draft : new ProductEditorDraft();
        // Match persisted IDs when client identities differ; unsaved IDs can never match saved variants.
        var persisted = draft.Attributes.SelectMany(a => a.Options).Where(o => o.Id > 0).ToDictionary(o => o.ClientId, o => o.Id);
        List<EditorCombination> rows = [];
        for (var current = offset; current < total && rows.Count < 50; current++)
        {
            var ordinal = current; Dictionary<int, Guid> choices = []; List<string> labels = [];
            foreach (var axis in axes.AsEnumerable().Reverse())
            {
                var value = axis.Values[(int)(ordinal % axis.Values.Count)]; ordinal /= axis.Values.Count;
                if (value is not null) choices[axis.Attribute.DefinitionId] = value.ClientId;
                labels.Insert(0, $"{axis.Attribute.Name}: {value?.Value ?? "Not applicable"}");
            }
            var key = ProductEditorRules.ChoiceKey(choices);
            var target = choices.Values.All(persisted.ContainsKey) ? ProductEditorRules.Signature(choices.Values.Select(c => persisted[c])) : "~new";
            var matches = existing.Variants.Where(v => ProductEditorRules.Signature(v.Choices.Values.Select(c => existing.Attributes.SelectMany(a => a.Options).Single(o => o.ClientId == c).Id)) == target).ToList();
            var staged = draft.Variants.Any(v => !v.Archived && ProductEditorRules.ChoiceKey(v.Choices) == key);
            var status = matches.Any(v => !v.Archived) || staged ? "Existing" : matches.Count > 0 ? "Archived" : "Missing";
            rows.Add(new(key, choices, labels.Count == 0 ? "Standard variant" : string.Join(" / ", labels), status, matches.Select(v => v.Id).ToList()));
        }
        return new(total.ToString(), offset.ToString(), offset + rows.Count < total, rows);
    }
}
