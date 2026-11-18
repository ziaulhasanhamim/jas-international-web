using System.Text.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
namespace JASIntWeb.Services.ProductEditor;

public sealed partial class ProductEditorService
{
    private async Task<Dictionary<Guid, ProductAttributeOption>> SaveAttributesAsync(int productId, List<EditorAttribute> rows, Guid operation, CancellationToken ct)
    {
        if (rows.Select(a => a.DefinitionId).Distinct().Count() != rows.Count) throw Invalid("Options", "Choose each attribute only once.");
        var definitions = await db.AttributeDefinitions.Include(a => a.Options).ToDictionaryAsync(a => a.Id, ct);
        var bindings = await db.ProductAttributeDefinitions.Include(a => a.Options).Where(a => a.ProductId == productId).ToListAsync(ct);
        var owned = bindings.SelectMany(a => a.Options).ToDictionary(o => o.Id);
        var originalNormal = owned.ToDictionary(p => p.Key, p => p.Value.NormalizedValue);
        // Permit swapping labels under unique constraints without an intermediate duplicate.
        foreach (var option in owned.Values) option.NormalizedValue = $"~{operation:N}-{option.Id}";
        await db.SaveChangesAsync(ct);
        Dictionary<Guid, ProductAttributeOption> mapped = [];
        HashSet<int> retained = [];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; var field = $"Options.{row.DefinitionId}";
            if (!definitions.TryGetValue(row.DefinitionId, out var definition)) throw Invalid(field, "This attribute definition no longer exists.");
            if (row.Options is null || row.Fields is null || row.Options.Any(o => o is null) || row.Options.Count(o => !o.Retired) > 100) throw Invalid(field, "Use at most 100 current values per attribute.");
            try { ProductEditorRules.ValidateFields(row.Fields, row.SwatchField); } catch (ArgumentException e) { throw Invalid(field, e.Message); }
            var binding = bindings.SingleOrDefault(a => a.AttributeDefinitionId == row.DefinitionId);
            if (binding is null)
            {
                binding = new() { ProductId = productId, AttributeDefinitionId = row.DefinitionId };
                db.ProductAttributeDefinitions.Add(binding);
            }
            var oldFields = ProductEditorRules.DecodeFields(binding.MetadataSchemaJson);
            foreach (var oldField in oldFields.Where(old => !row.Fields.Any(next => next.Key == old.Key && next.Kind == old.Kind)))
            {
                if (binding.Options.Any(option =>
                    row.Options.SingleOrDefault(o => o.Id == option.Id)?.Metadata.GetValueOrDefault(oldField.Key)
                    is { Length: > 0 } || !row.Options.Any(o => o.Id == option.Id) && ProductEditorRules.DecodeMetadata(option.MetadataJson).ContainsKey(oldField.Key)))
                    throw Invalid(field, $"Clear {oldField.Label} on all values before deleting its field or changing its type/key.");
            }
            binding.MetadataSchemaJson = JsonSerializer.Serialize(row.Fields);
            binding.SwatchFieldKey = string.IsNullOrWhiteSpace(row.SwatchField) ? null : row.SwatchField;
            binding.IsRetired = row.Retired;
            binding.SortOrder = index;
            binding.IsRequired = row.Required;
            HashSet<string> normalized = [];
            for (var position = 0; position < row.Options.Count; position++)
            {
                var input = row.Options[position]; var optionField = $"{field}.{input.ClientId:N}";
                if (input.ClientId == Guid.Empty || input.Metadata is null || string.IsNullOrWhiteSpace(input.Value) || input.Value.Trim().Length > 200)
                    throw Invalid(optionField, "Enter a value label of 1–200 characters.");
                var label = input.Value.Trim(); var normal = ProductWorkspaceService.NormalizeValue(label);
                if (!normalized.Add(normal)) throw Invalid(optionField, "Values must be unique within this attribute, including retired values.");
                ProductAttributeOption entity;
                if (input.Id > 0)
                {
                    if (!owned.TryGetValue(input.Id, out var found) || found.AttributeDefinitionId != row.DefinitionId || !retained.Add(input.Id))
                        throw Invalid(optionField, "An option belongs to another product/attribute or appears more than once.");
                    entity = found;
                }
                else
                {
                    if (input.SuggestionId is { } source && !definition.Options.Any(o => o.Id == source)) throw Invalid(optionField, "The suggestion belongs to another attribute.");
                    var code = EditorSlug.FromName(label);
                    if (string.IsNullOrWhiteSpace(code)) code = "value";
                    if (code.Length > 80) code = code[..80];
                    var candidate = code;
                    for (var suffix = 2; binding.Options.Any(o => o.Code == candidate); suffix++) candidate = $"{code}-{suffix}";
                    entity = new() { ProductId = productId, AttributeDefinitionId = row.DefinitionId, AttributeOptionId = input.SuggestionId, Code = candidate };
                    binding.Options.Add(entity);
                }
                try { entity.MetadataJson = ProductEditorRules.EncodeMetadata(row.Fields, input.Metadata); }
                catch (ArgumentException e) { throw Invalid(optionField, e.Message); }
                entity.Value = label; entity.NormalizedValue = normal; entity.SortOrder = position; entity.IsRetired = input.Retired || row.Retired;
                if (!mapped.TryAdd(input.ClientId, entity)) throw Invalid(optionField, "An option has a duplicate client identity.");
            }
        }
        foreach (var option in owned.Values.Where(o => !retained.Contains(o.Id)))
        { option.NormalizedValue = originalNormal[option.Id]; option.IsRetired = true; }
        foreach (var binding in bindings.Where(a => !rows.Any(row => row.DefinitionId == a.AttributeDefinitionId)))
        { binding.IsRetired = true; foreach (var option in binding.Options) option.IsRetired = true; }
        // The DB also protects normalized labels/codes and cross-product ownership.
        await db.SaveChangesAsync(ct);
        return mapped;
    }
}
