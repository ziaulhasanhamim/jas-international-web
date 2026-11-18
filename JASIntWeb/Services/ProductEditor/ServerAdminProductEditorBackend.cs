using JASIntWeb.Client.Contracts;
namespace JASIntWeb.Services.ProductEditor;

public sealed class ServerAdminProductEditorBackend(AdminSession session, IServiceScopeFactory scopes, ILogger<ServerAdminProductEditorBackend> logger) : IAdminProductEditorBackend
{
    private async Task<T> Execute<T>(Func<ProductEditorService, int, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            return await session.RunAsync(async (_, actor) =>
            {
                await using var scope = scopes.CreateAsyncScope();
                // Cancellation flows through the command; do not dispose its scope while a transaction is still running.
                return await action(scope.ServiceProvider.GetRequiredService<ProductEditorService>(), actor, timeout.Token);
            });
        }
        catch (AdminCatalogException) { throw; }
        catch (ShopException e) { throw new AdminCatalogException(e.Message.Contains("session expired", StringComparison.OrdinalIgnoreCase) ? 401 : 409, e.Message); }
        catch (OperationCanceledException) { throw new AdminCatalogException(504, "The request timed out. Check the operation status before retrying; your draft is retained."); }
        catch (Exception e)
        {
            logger.LogError(e, "Product editor command failed");
            throw new AdminCatalogException(500, "The operation could not be confirmed. Your draft is retained; check its status before retrying.");
        }
    }
    public Task<EditorTemplateData> TemplateAsync(int id, CancellationToken ct = default) => Execute((s, _, token) => s.TemplateAsync(id, token), ct);
    public Task<EditorTemplateData> SaveTemplateAsync(EditorTemplateData command, CancellationToken ct = default) => Execute((s, _, token) => s.SaveTemplateAsync(command, token), ct);
    public Task<EditorSuggestionData> SuggestionAsync(int id, CancellationToken ct = default) => Execute((s, _, token) => s.SuggestionAsync(id, token), ct);
    public Task<EditorSuggestionData> SaveSuggestionAsync(EditorSuggestionData command, CancellationToken ct = default) => Execute((s, _, token) => s.SaveSuggestionAsync(command, token), ct);
    public Task<ProductEditorData> LoadAsync(int id, CancellationToken ct = default) => Execute((s, _, token) => s.LoadAsync(id, token), ct);
    public Task<EditorCombinationPage> PreviewAsync(EditorCombinationQuery query, CancellationToken ct = default) => Execute((s, _, token) => s.PreviewAsync(query, token), ct);
    public Task<EditorCommit> SaveAsync(EditorSave command, CancellationToken ct = default) => Execute((s, actor, token) => s.SaveAsync(actor, command, token), ct);
    public Task<EditorCommit> AdjustStockAsync(EditorStock command, CancellationToken ct = default) => Execute((s, actor, token) => s.StockAsync(actor, command, token), ct);
    public Task<EditorCommit?> OperationAsync(Guid operation, CancellationToken ct = default) => Execute((s, actor, token) => s.OperationAsync(actor, operation, token), ct);
}
