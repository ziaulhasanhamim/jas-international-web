#pragma warning disable BL0016 // Typed interop wrapper propagates failures to component fallback handlers.
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
namespace JASIntWeb.Client.Services;

public sealed record RichEditorOptions(int Height, bool Specifications, bool Disabled, string Label);

// Each component owns one handle. Modules use local listeners and expose deterministic cleanup.
public sealed class AdminEditorInterop(IJSRuntime js) : IAsyncDisposable
{
    private const string Version = "20261121v11";
    private IJSObjectReference? module;
    private IJSObjectReference? control;
    private bool disposed;

    public async Task RichTextAsync<T>(ElementReference element, DotNetObjectReference<T> receiver, RichEditorOptions options) where T : class
    {
        module = await js.InvokeAsync<IJSObjectReference>("import", $"/js/modules/admin-rich-text.js?v={Version}");
        if (disposed) { await module.DisposeAsync(); module = null; return; }
        control = await module.InvokeAsync<IJSObjectReference>("create", element, receiver, options);
        if (disposed) await DisposeControlAsync();
    }
    public async Task DropZoneAsync(ElementReference element)
    {
        module = await js.InvokeAsync<IJSObjectReference>("import", $"/js/modules/admin-media.js?v={Version}");
        if (disposed) { await module.DisposeAsync(); module = null; return; }
        control = await module.InvokeAsync<IJSObjectReference>("dropZone", element);
        if (disposed) await DisposeControlAsync();
    }
    public async Task GalleryAsync<T>(ElementReference element, DotNetObjectReference<T> receiver) where T : class
    {
        module = await js.InvokeAsync<IJSObjectReference>("import", $"/js/modules/admin-media.js?v={Version}");
        if (disposed) { await module.DisposeAsync(); module = null; return; }
        control = await module.InvokeAsync<IJSObjectReference>("gallery", element, receiver);
        if (disposed) await DisposeControlAsync();
    }
    public async Task UpdateRichTextAsync(string html, bool disabled)
    {
        if (!disposed && control is not null) await control.InvokeVoidAsync("update", html, disabled);
    }
    private async Task DisposeControlAsync()
    {
        if (control is null) return;
        await control.InvokeVoidAsync("dispose");
        await control.DisposeAsync();
        control = null;
    }
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        try { await DisposeControlAsync(); if (module is not null) await module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
        catch (TaskCanceledException) { }
        module = null;
    }
}
