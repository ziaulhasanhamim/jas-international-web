using JASIntWeb.Client.Contracts;

namespace JASIntWeb.Services;

public sealed class ServerAdminProductWorkspaceBackend(AdminSession session, IServiceScopeFactory scopes) : IAdminProductWorkspaceBackend
{
    private async Task<T> Execute<T>(Func<ProductWorkspaceService, Task<T>> action)
    {
        try
        {
            return await session.RunAsync(async (_, _) =>
            {
                await using var scope = scopes.CreateAsyncScope();
                return await action(scope.ServiceProvider.GetRequiredService<ProductWorkspaceService>());
            });
        }
        catch (ShopException error)
        {
            throw new AdminCatalogException(error.Message.Contains("session expired", StringComparison.OrdinalIgnoreCase) ? 401 : 409, error.Message);
        }
    }
    public Task<ProductWorkspace> LoadAsync(int productId) => Execute(service => service.LoadAsync(productId));
    public Task<ProductWorkspace> SaveConfigurationAsync(int productId, ProductConfiguration command) => throw new AdminCatalogException(410, "The product editor has been updated. Reload and use Save product.");
    public Task<ProductWorkspace> SaveVariantAsync(int productId, WorkspaceVariant command) => throw new AdminCatalogException(410, "The product editor has been updated. Reload and use Save product.");
    public Task<ProductWorkspace> AdjustStockAsync(int productId, WorkspaceStock command) => throw new AdminCatalogException(410, "The product editor has been updated. Reload and use Save product.");
    public Task<CombinationPreview> PreviewAsync(int productId, long revision) => throw new AdminCatalogException(410, "The product editor has been updated. Reload and use Save product.");
    public Task<ProductWorkspace> GenerateAsync(int productId, long revision) => throw new AdminCatalogException(410, "The product editor has been updated. Reload and use Save product.");
}
