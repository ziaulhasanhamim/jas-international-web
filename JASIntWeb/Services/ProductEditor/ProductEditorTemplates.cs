using System.Text.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
namespace JASIntWeb.Services.ProductEditor;

public sealed partial class ProductEditorService
{
    public async Task<EditorTemplateData> TemplateAsync(int id, CancellationToken ct)
    {
        var row = await db.AttributeDefinitions.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new ProductEditorException(404, "Attribute not found.");
        return new(id, ProductEditorRules.DecodeFields(row.MetadataSchemaJson), row.SwatchFieldKey);
    }
    public async Task<EditorTemplateData> SaveTemplateAsync(EditorTemplateData command, CancellationToken ct)
    {
        if (command.Fields is null) throw Invalid("Fields", "Provide field definitions.");
        try { ProductEditorRules.ValidateFields(command.Fields, command.SwatchField); } catch (ArgumentException e) { throw Invalid("Fields", e.Message); }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"AttributeDefinitions\" WHERE \"Id\" = {command.Id} FOR UPDATE", ct);
        var row = await db.AttributeDefinitions.Include(a => a.Options).SingleOrDefaultAsync(a => a.Id == command.Id, ct) ?? throw new ProductEditorException(404, "Attribute not found.");
        foreach (var field in ProductEditorRules.DecodeFields(row.MetadataSchemaJson).Where(f => !command.Fields.Any(n => n.Key == f.Key && n.Kind == f.Kind)))
            if (row.Options.Any(o => ProductEditorRules.DecodeMetadata(o.MetadataJson).ContainsKey(field.Key))) throw Invalid("Fields", $"Clear {field.Label} on all suggestions before changing its key/type or removing it.");
        row.MetadataSchemaJson = JsonSerializer.Serialize(command.Fields); row.SwatchFieldKey = command.SwatchField;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return command;
    }
    public async Task<EditorSuggestionData> SuggestionAsync(int id, CancellationToken ct)
    {
        var row = await db.AttributeOptions.AsNoTracking().Include(a => a.AttributeDefinition).SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new ProductEditorException(404, "Suggestion not found.");
        return new(id, ProductEditorRules.DecodeFields(row.AttributeDefinition!.MetadataSchemaJson), ProductEditorRules.DecodeMetadata(row.MetadataJson));
    }
    public async Task<EditorSuggestionData> SaveSuggestionAsync(EditorSuggestionData command, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var definitionId = await db.AttributeOptions.Where(a => a.Id == command.Id).Select(a => a.AttributeDefinitionId).SingleOrDefaultAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"AttributeDefinitions\" WHERE \"Id\" = {definitionId} FOR UPDATE", ct);
        var row = await db.AttributeOptions.Include(a => a.AttributeDefinition).SingleOrDefaultAsync(a => a.Id == command.Id, ct) ?? throw new ProductEditorException(404, "Suggestion not found.");
        var fields = ProductEditorRules.DecodeFields(row.AttributeDefinition!.MetadataSchemaJson);
        if (command.Values is null) throw Invalid("Values", "Provide suggestion values.");
        try { row.MetadataJson = ProductEditorRules.EncodeMetadata(fields, command.Values); } catch (ArgumentException e) { throw Invalid("Values", e.Message); }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(command.Id, fields, ProductEditorRules.DecodeMetadata(row.MetadataJson));
    }
}
