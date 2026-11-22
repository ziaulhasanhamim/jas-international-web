using JASIntWeb.Client.Contracts;
using JASIntWeb.Helpers;
namespace JASIntWeb.Services.ProductEditor;

public sealed class ServerAdminProductEditorBackend(AdminSession session, IServiceScopeFactory scopes, ILogger<ServerAdminProductEditorBackend> logger) : IAdminProductEditorBackend
{
    private async Task<Result<T>> Execute<T>(Func<ProductEditorService, int, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var res = await session.RunAsync(async (_, actor) =>
            {
                await using var scope = scopes.CreateAsyncScope();
                // Cancellation flows through the command; do not dispose its scope while a transaction is still running.
                return await action(scope.ServiceProvider.GetRequiredService<ProductEditorService>(), actor, timeout.Token);
            });
            return res switch
            {
                Result<T> r => r,
                T val => val,
                Error err => err
            };
        }
        catch (ProductEditorException e) { return new CatalogError(e.Message, e.Status, e.Errors); }
        catch (OperationCanceledException) { return new CatalogError("The request timed out. Check the operation status before retrying; your draft is retained.", 504); }
        catch (Exception e)
        {
            logger.LogError(e, "Product editor command failed");
            return new CatalogError("The operation could not be confirmed. Your draft is retained; check its status before retrying.", 500);
        }
    }
    public Task<Result<EditorTemplateData>> TemplateAsync(int id, CancellationToken ct = default) => Execute((s, _, token) => s.TemplateAsync(id, token), ct);
    public Task<Result<EditorTemplateData>> SaveTemplateAsync(EditorTemplateData command, CancellationToken ct = default) => Execute((s, _, token) => s.SaveTemplateAsync(command, token), ct);
    public Task<Result<EditorSuggestionData>> SuggestionAsync(int id, CancellationToken ct = default) => Execute((s, _, token) => s.SuggestionAsync(id, token), ct);
    public Task<Result<EditorSuggestionData>> SaveSuggestionAsync(EditorSuggestionData command, CancellationToken ct = default) => Execute((s, _, token) => s.SaveSuggestionAsync(command, token), ct);
    public Task<Result<ProductEditorData>> LoadAsync(int id, CancellationToken ct = default) => Execute((s, _, token) => s.LoadAsync(id, token), ct);
    public Task<Result<EditorCombinationPage>> PreviewAsync(EditorCombinationQuery query, CancellationToken ct = default) => Execute((s, _, token) => s.PreviewAsync(query, token), ct);
    public Task<Result<EditorCommit>> SaveAsync(EditorSave command, CancellationToken ct = default) => Execute((s, actor, token) => s.SaveAsync(actor, command, token), ct);
    public Task<Result<EditorCommit>> AdjustStockAsync(EditorStock command, CancellationToken ct = default) => Execute((s, actor, token) => s.StockAsync(actor, command, token), ct);
    public Task<Result<EditorCommit?>> OperationAsync(Guid operation, CancellationToken ct = default) => Execute((s, actor, token) => s.OperationAsync(actor, operation, token), ct);
}