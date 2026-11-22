using JASIntWeb.Client.Contracts;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Services;

public enum EditorPhase { Loading, Ready, Saving, Reconciling, Saved, Validation, Conflict, SessionExpired, ConnectionError, Failed }
public sealed class ProductEditorSession
{
    public ProductEditorData? Data { get; private set; }
    public ProductEditorDraft Draft { get; private set; } = new();
    public EditorPhase Phase { get; set; } = EditorPhase.Loading;
    public bool Dirty { get; private set; }
    public string? Message { get; set; }
    public Dictionary<string, string[]> Errors { get; set; } = [];
    public bool Busy => Phase is EditorPhase.Loading or EditorPhase.Saving or EditorPhase.Reconciling;
    public void Load(ProductEditorData data)
    { Data = data; Draft = ProductEditorRules.Copy(data.Draft); Dirty = false; Phase = EditorPhase.Ready; Message = null; Errors.Clear(); }
    public void UpdateTemplates(List<EditorAttributeTemplate> templates) { if (Data is not null) Data = Data with { Templates = templates }; }
    public void Restore(ProductEditorDraft draft) { Draft = ProductEditorRules.Copy(draft); Dirty = true; }
    public void Change(Action<ProductEditorDraft> update)
    { update(Draft); Dirty = true; Message = null; Errors.Clear(); if (!Busy) Phase = EditorPhase.Ready; }
    public void Accepted(EditorCommit commit)
    {
        if (commit.Editor is not null) Load(commit.Editor);
        else { Draft.Id = commit.Receipt.ProductId; Draft.Revision = commit.Receipt.Revision; Dirty = false; }
        Phase = EditorPhase.Saved; Message = commit.RefreshWarning ?? "Product saved.";
    }
    public void Reject(Error error)
    {
        Message = error.Message; Errors = error.ValidationErrors ?? [];
        Phase = error.Code switch { 401 or 403 => EditorPhase.SessionExpired, 409 => EditorPhase.Conflict, 400 or 422 => EditorPhase.Validation, 504 => EditorPhase.ConnectionError, _ => EditorPhase.Failed };
    }
    public void Stage(IEnumerable<EditorCombination> combinations)
    {
        var added = combinations.Where(c => c.Status == "Missing" && !Draft.Variants.Any(v => !v.Archived && ProductEditorRules.ChoiceKey(v.Choices) == c.Key)).ToList();
        if (added.Count + Draft.Variants.Count(v => !v.Archived) > 100) throw new ArgumentException("A product supports at most 100 current variants.");
        Change(draft => { foreach (var row in added) draft.Variants.Add(new() { Choices = new(row.Choices) }); });
    }
}
public sealed record InformationPatch(Action<ProductInformation> Apply);
public sealed record AttributePatch(int DefinitionId, Action<EditorAttribute> Apply);
public sealed record VariantPatch(Guid ClientId, Action<EditorVariant> Apply);
public sealed record MetadataPatch(string Key, string Value);
public sealed record PreviewRequest(string Offset, Dictionary<int, List<Guid>> Filters);
public sealed record CombinationMapping(Guid VariantId, EditorCombination Combination);
