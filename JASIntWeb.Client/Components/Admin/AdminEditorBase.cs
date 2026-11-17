using JASIntWeb.Client.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
namespace JASIntWeb.Client.Components.Admin;

public abstract class AdminEditorBase<T> : ComponentBase, IDisposable where T : class
{
    [Parameter] public int Id { get; set; }
    [Inject] protected IAdminCatalogBackend Backend { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] private PersistentComponentState State { get; set; } = default!;
    protected T? Input;
    protected string? error;
    protected bool notFound;
    protected AdminEditorData<T>? Snapshot;
    protected EditContext? EditContext;
    private ValidationMessageStore? messages;
    private PersistingComponentStateSubscription? subscription;
    protected bool saving;
    protected bool AccessDenied;
    private int uploads;
    protected bool Busy => AccessDenied || saving || uploads > 0 || !RendererInfo.IsInteractive;
    protected abstract Task<AdminEditorData<T>> LoadAsync();
    protected abstract Task<AdminSaveResult> SaveAsync(T input);
    protected override async Task OnInitializedAsync()
    {
        var key = $"admin-editor:{GetType().Name}:{Id}";
        try
        {
            if (State.TryTakeFromJson<AdminEditorData<T>>(key, out var existing)) Snapshot = existing;
            Snapshot ??= await LoadAsync();
            Input = Snapshot!.Input;
            EditContext = new(Input);
            messages = new(EditContext);
            EditContext.OnValidationRequested += ClearServerValidation;
            subscription = State.RegisterOnPersisting(() => { State.PersistAsJson(key, Snapshot); return Task.CompletedTask; }, new Microsoft.AspNetCore.Components.Web.InteractiveAutoRenderMode());
        }
        catch (AdminCatalogException failure)
        {
            notFound = failure.Status == 404;
            AccessDenied = failure.Status is 401 or 403;
            error = failure.Message;
        }
        catch (HttpRequestException)
        {
            error = "The editor could not be loaded. Check your connection and reload this page.";
        }
    }
    protected async Task Save()
    {
        if (Busy || Input is null) return;
        saving = true;
        error = null;
        messages?.Clear();
        try
        {
            var saved = await SaveAsync(Input);
            Nav.NavigateTo(saved.RedirectUrl);
        }
        catch (AdminCatalogException failure)
        {
            AccessDenied = failure.Status is 401 or 403;
            error = failure.Message;
            if (failure.Errors is not null && EditContext is not null)
                foreach (var (name, errors) in failure.Errors) messages!.Add(EditContext.Field(name), errors);
            EditContext?.NotifyValidationStateChanged();
        }
        catch (HttpRequestException) { error = "The connection failed. Your entered values are still here. Try again."; }
        finally { saving = false; }
    }
    protected void UploadBusy(bool busy) { uploads = Math.Max(0, uploads + (busy ? 1 : -1)); }
    private void ClearServerValidation(object? sender, ValidationRequestedEventArgs args) => messages?.Clear();
    public void Dispose()
    {
        if (EditContext is not null) EditContext.OnValidationRequested -= ClearServerValidation;
        subscription?.Dispose();
    }
}
