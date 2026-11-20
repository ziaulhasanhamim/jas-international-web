using System.Text.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace JASIntWeb.Client.Components.Admin;

public partial class ProductEditorForm
{
    [Parameter] public int Id { get; set; }
    [Parameter] public string InitialTab { get; set; } = "details";
    [Parameter] public bool ContentTab { get; set; }
    [Inject] private IAdminProductEditorBackend Backend { get; set; } = default!;
    [Inject] private PersistentComponentState Persistent { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? Authentication { get; set; }
    private readonly ProductEditorSession state = new();
    private readonly Dictionary<string, string> sections = new() { ["details"] = "Details", ["options"] = "Options", ["variants"] = "Variants", ["content"] = "Content & SEO" };
    private string tab = "details", requestedTab = "";
    private int loaded = -1, uploads;
    private bool disposed, uncertain, restored, reloadRequired, retryAllowed, restoredFromDraft;
    private CancellationTokenSource lifetime = new();
    private PersistingComponentStateSubscription subscription;
    private EditorCombinationPage? preview;
    private EditorVariant? stockVariant;
    private Guid? pendingStock;
    private string? stockMessage;
    private ProductEditorData? latest;
    private EditorSave? submitted;
    private EditorStock? submittedStock;
    private IJSObjectReference? browser;
    private string storageKey = "";
    private string DraftStorageKey => $"jas-admin-draft:product:{Id}";
    private bool Disabled => state.Busy || reloadRequired || uploads > 0 || !RendererInfo.IsInteractive;
    private record Pending(EditorSave? Product, EditorStock? Stock);
    protected override void OnInitialized() => subscription = Persistent.RegisterOnPersisting(() =>
    {
        if (state.Data is not null) Persistent.PersistAsJson($"product-editor-v2:{Id}", state.Data);
        return Task.CompletedTask;
    }, new InteractiveAutoRenderMode());
    protected override async Task OnParametersSetAsync()
    {
        if (loaded == Id) { if (requestedTab != InitialTab && sections.ContainsKey(InitialTab)) tab = InitialTab; requestedTab = InitialTab; return; }
        requestedTab = InitialTab;
        loaded = Id; restored = false; uncertain = false; restoredFromDraft = false; tab = ContentTab ? "content" : sections.ContainsKey(InitialTab) ? InitialTab : "details";
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); preview = null; stockVariant = null; submitted = null; submittedStock = null;
        state.Phase = EditorPhase.Loading;
        try
        {
            var data = Persistent.TryTakeFromJson<ProductEditorData>($"product-editor-v2:{Id}", out var cached) && cached is not null ? cached : await Backend.LoadAsync(Id, lifetime.Token);
            if (!disposed) state.Load(data);
        }
        catch (Exception e) { Failure(e); }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || restored || state.Data is null) return;
        restored = true;
        try
        {
            browser ??= await JS.InvokeAsync<IJSObjectReference>("import", "/js/modules/product-editor.js");
            var user = (await (Authentication ?? Task.FromResult(new AuthenticationState(new())))).User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "admin";
            storageKey = $"jas-product-pending:{user}:{Id}";
            var json = await browser.InvokeAsync<string?>("read", storageKey);
            if (json is not null && JsonSerializer.Deserialize<Pending>(json) is { } pending)
            {
                submitted = pending.Product; submittedStock = pending.Stock; uncertain = true; retryAllowed = false;
                if (submitted is not null) state.Restore(submitted.Draft);
                state.Phase = EditorPhase.ConnectionError; state.Message = "An earlier request needs confirmation. Check its status before making another save.";
                StateHasChanged();
                return;
            }

            // Check for unsaved local storage draft
            var draftJson = await JS.InvokeAsync<string?>("jasAdminDrafts.get", DraftStorageKey);
            if (!string.IsNullOrWhiteSpace(draftJson))
            {
                var draft = JsonSerializer.Deserialize<ProductEditorDraft>(draftJson);
                if (draft is not null && HasDraftDifferences(draft, state.Data.Draft))
                {
                    state.Restore(draft);
                    restoredFromDraft = true;
                    StateHasChanged();
                }
            }
        }
        catch (Exception e) when (e is JSException or JSDisconnectedException or JsonException) { /* Core editing remains available without optional session storage. */ }
    }
    private void Change(Action<ProductEditorDraft> update, bool invalidate = false)
    {
        state.Change(update);
        if (invalidate) preview = null;
        PersistDraft();
    }
    private void PersistDraft()
    {
        if (!state.Dirty || !RendererInfo.IsInteractive || disposed) return;
        try
        {
            var json = JsonSerializer.Serialize(state.Draft);
            _ = JS.InvokeVoidAsync("jasAdminDrafts.save", DraftStorageKey, json);
        }
        catch (Exception) { /* Non-blocking */ }
    }
    private void InfoChanged(InformationPatch patch) => Change(d => patch.Apply(d.Information));
    private void AttributeChanged(AttributePatch patch) => Change(d => patch.Apply(d.Attributes.Single(a => a.DefinitionId == patch.DefinitionId)), true);
    private void VariantChanged(VariantPatch patch) { Change(d => patch.Apply(d.Variants.Single(v => v.ClientId == patch.ClientId))); RefreshPreview(); }
    private void AddAttribute(EditorAttribute attribute) => Change(d => { var old = d.Attributes.FirstOrDefault(a => a.DefinitionId == attribute.DefinitionId); if (old is null) d.Attributes.Add(attribute); else old.Retired = false; }, true);
    private void MoveAttribute((int Id, int Step) move) => Change(d => { var index = d.Attributes.FindIndex(a => a.DefinitionId == move.Id); var next = index + move.Step; if (next < 0 || next >= d.Attributes.Count) return; var row = d.Attributes[index]; d.Attributes.RemoveAt(index); d.Attributes.Insert(next, row); }, true);
    private void SetDefault((bool Selected, Guid Key) change) => Change(d => { foreach (var v in d.Variants) if (v.ClientId == change.Key || change.Selected) v.Default = v.ClientId == change.Key && change.Selected; });
    private void AddStandard() => Change(d => d.Variants.Add(new() { Default = true }));
    private void ArchiveVariant(Guid key) { Change(d => { var v = d.Variants.Single(v => v.ClientId == key); if (v.Id == 0) d.Variants.Remove(v); else { v.Archived = !v.Archived; v.Default = false; v.Active = false; } }); RefreshPreview(); }
    private void RestoreVariant(int id) { var v = state.Draft.Variants.SingleOrDefault(v => v.Id == id); if (v is not null) ArchiveVariant(v.ClientId); }
    private void MapVariant(CombinationMapping mapping) { Change(d => d.Variants.Single(v => v.ClientId == mapping.VariantId).Choices = new(mapping.Combination.Choices)); RefreshPreview(); }
    private void Stage(List<EditorCombination> rows) { try { state.Stage(rows); RefreshPreview(); } catch (ArgumentException e) { state.Message = e.Message; state.Phase = EditorPhase.Validation; } }
    private void RefreshPreview() { if (preview is not null) preview = preview with { Rows = preview.Rows.Select(r => r with { Status = state.Draft.Variants.Any(v => !v.Archived && ProductEditorRules.ChoiceKey(v.Choices) == r.Key) ? "Existing" : r.Status == "Existing" ? "Missing" : r.Status }).ToList() }; }
    private async Task Preview(PreviewRequest request)
    {
        if (Disabled) return; state.Phase = EditorPhase.Saving; state.Message = null;
        try { preview = await Backend.PreviewAsync(new(ProductEditorRules.Copy(state.Draft), request.Offset, request.Filters), lifetime.Token); state.Phase = EditorPhase.Ready; }
        catch (Exception e) { Failure(e); }
    }
    private async Task Save()
    {
        if (Disabled || uncertain) return;
        submitted = new(Guid.NewGuid(), ProductEditorRules.Copy(state.Draft)); submittedStock = null;
        await Execute();
    }
    private async Task Execute()
    {
        state.Phase = EditorPhase.Saving; state.Message = null;
        await Remember();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var result = submittedStock is not null ? await Backend.AdjustStockAsync(submittedStock, timeout.Token) : await Backend.SaveAsync(submitted!, timeout.Token);
            await Accept(result);
        }
        catch (Exception e)
        {
            Failure(e);
            retryAllowed = false;
            uncertain = e is not AdminCatalogException error || error.Status is >= 500 or 401 or 403;
            if (!uncertain) await Forget();
        }
        finally { if (state.Busy) state.Phase = EditorPhase.Failed; }
    }
    private async Task Accept(EditorCommit result)
    {
        var isNew = Id == 0;
        state.Accepted(result); reloadRequired = result.Editor is null; uncertain = false; stockVariant = null; stockMessage = null; preview = null; restoredFromDraft = false;
        await Forget();
        try { await JS.InvokeVoidAsync("jasAdminDrafts.remove", DraftStorageKey); } catch (Exception) { }
        if (isNew) { try { await JS.InvokeVoidAsync("jasAdminDrafts.remove", "jas-admin-draft:product:0"); } catch (Exception) { } }
        submitted = null; submittedStock = null;
        if (isNew) Nav.NavigateTo($"/admin/products/{result.Receipt.ProductId}/edit", replace: true);
    }
    private async Task CheckOperation()
    {
        if (state.Busy) return; state.Phase = EditorPhase.Reconciling;
        try
        {
            var result = await Backend.OperationAsync(submitted?.OperationId ?? submittedStock!.OperationId, lifetime.Token);
            if (result is not null) await Accept(result);
            else { retryAllowed = true; state.Phase = EditorPhase.ConnectionError; state.Message = "No completed operation found yet. Retry the same request safely."; }
        }
        catch (Exception e) { Failure(e); }
        finally { if (state.Busy) state.Phase = EditorPhase.ConnectionError; }
    }
    private Task Retry() => state.Busy || !retryAllowed ? Task.CompletedTask : Execute();
    private async Task OpenStock(Guid key)
    { pendingStock = key; if (state.Dirty) return; await LoadStock(key); }
    private async Task SaveAndOpenStock() { var key = pendingStock; await Save(); if (state.Phase == EditorPhase.Saved && key is { } value) await LoadStock(value); }
    private async Task LoadStock(Guid key)
    {
        state.Phase = EditorPhase.Loading;
        try { var fresh = await Backend.LoadAsync(state.Draft.Id, lifetime.Token); state.Load(fresh); stockVariant = fresh.Draft.Variants.Single(v => v.ClientId == key); pendingStock = null; stockMessage = null; }
        catch (Exception e) { Failure(e); }
    }
    private async Task Stock(EditorStock command)
    {
        if (uncertain || state.Busy) return;
        submittedStock = command; submitted = null; await Execute();
        if (stockVariant is not null) stockMessage = state.Message;
    }
    private async Task RefreshStock()
    {
        if (stockVariant is null) return;
        try { var fresh = await Backend.LoadAsync(state.Draft.Id, lifetime.Token); stockVariant = fresh.Draft.Variants.Single(v => v.Id == stockVariant.Id); stockMessage = "Current stock loaded. Review your retained quantity and reason before confirming."; }
        catch (Exception e) { Failure(e); stockMessage = state.Message; }
    }
    private async Task RefreshTemplates() { try { var fresh = await Backend.LoadAsync(state.Draft.Id, lifetime.Token); state.UpdateTemplates(fresh.Templates); } catch (Exception e) { Failure(e); } }
    private async Task ReviewLatest() { try { latest = await Backend.LoadAsync(state.Draft.Id, lifetime.Token); } catch (Exception e) { Failure(e); } }
    private async Task UseLatest() { if (latest is not null && await Confirm("Replace your retained draft with the latest saved product? Download it first if needed.")) { state.Load(latest); latest = null; preview = null; } }
    private async Task Reload() { try { state.Load(await Backend.LoadAsync(state.Draft.Id == 0 ? Id : state.Draft.Id, lifetime.Token)); reloadRequired = false; } catch (Exception e) { Failure(e); } }
    private void UploadBusy(bool busy) => uploads = Math.Max(0, uploads + (busy ? 1 : -1));
    private void Failure(Exception e)
    {
        if (disposed) return;
        if (e is AdminCatalogException error) state.Reject(error);
        else { state.Phase = e is HttpRequestException or OperationCanceledException ? EditorPhase.ConnectionError : EditorPhase.Failed; state.Message = "The operation could not be confirmed. Your draft is retained. Check the operation status before retrying."; }
    }
    private async Task DiscardDraft()
    {
        try { await JS.InvokeVoidAsync("jasAdminDrafts.remove", DraftStorageKey); } catch (Exception) { }
        if (Id == 0) { try { await JS.InvokeVoidAsync("jasAdminDrafts.remove", "jas-admin-draft:product:0"); } catch (Exception) { } }
        if (state.Data is not null)
        {
            state.Load(state.Data);
            restoredFromDraft = false;
            preview = null;
        }
    }
    private static bool HasDraftDifferences(ProductEditorDraft draft, ProductEditorDraft? baseline)
    {
        if (baseline is null) return true;
        return JsonSerializer.Serialize(draft) != JsonSerializer.Serialize(baseline);
    }
    private async Task Remember() { try { if (browser is not null) await browser.InvokeVoidAsync("remember", storageKey, JsonSerializer.Serialize(new Pending(submitted, submittedStock))); } catch (JSException) { } }
    private async Task Forget() { try { if (browser is not null) await browser.InvokeVoidAsync("forget", storageKey); } catch (JSException) { } }
    private async Task Export() { try { browser ??= await JS.InvokeAsync<IJSObjectReference>("import", "/js/modules/product-editor.js"); await browser.InvokeVoidAsync("exportDraft", JsonSerializer.Serialize(new { state.Draft, Pending = new Pending(submitted, submittedStock) })); } catch (JSException) { state.Message = "Draft download is unavailable. Keep this tab open and retry."; } }
    private async Task<bool> Confirm(string message) { try { return await JS.InvokeAsync<bool>("confirm", message); } catch (JSException) { return false; } }
    private Task BeforeNavigation(LocationChangingContext context)
    {
        if (state.Dirty)
        {
            PersistDraft();
        }
        return Task.CompletedTask;
    }
    public async ValueTask DisposeAsync()
    { disposed = true; lifetime.Cancel(); lifetime.Dispose(); subscription.Dispose(); if (browser is not null) try { await browser.DisposeAsync(); } catch (JSDisconnectedException) { } }
}
