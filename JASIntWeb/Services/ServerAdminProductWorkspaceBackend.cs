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
    public Task<ProductWorkspace> SaveConfigurationAsync(int productId, ProductConfiguration command) => Execute(service => service.SaveConfigurationAsync(productId, command));
    public Task<ProductWorkspace> SaveVariantAsync(int productId, WorkspaceVariant command) => Execute(service => service.SaveVariantAsync(productId, command));
    public Task<ProductWorkspace> AdjustStockAsync(int productId, WorkspaceStock command) => Execute(service => service.AdjustStockAsync(productId, command));
    public Task<CombinationPreview> PreviewAsync(int productId, long revision) => Execute(service => service.PreviewAsync(productId, revision));
    public Task<ProductWorkspace> GenerateAsync(int productId, long revision) => Execute(service => service.GenerateAsync(productId, revision));
}
