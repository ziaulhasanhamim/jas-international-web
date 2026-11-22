using JASIntWeb.Client.Contracts;
using JASIntWeb.Helpers;

namespace JASIntWeb.Services;

public sealed class ServerAdminProductWorkspaceBackend(AdminSession session, IServiceScopeFactory scopes) : IAdminProductWorkspaceBackend
{
    private Task<Result<T>> Execute<T>(Func<ProductWorkspaceService, Task<Result<T>>> action) =>
        session.RunAsync(async (_, _) =>
        {
            await using var scope = scopes.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<ProductWorkspaceService>());
        });

    public Task<Result<ProductWorkspace>> LoadAsync(int productId) => Execute(service => service.LoadAsync(productId));
    public Task<Result<ProductWorkspace>> SaveConfigurationAsync(int productId, ProductConfiguration command) =>
        Task.FromResult<Result<ProductWorkspace>>(new Error("The product editor has been updated. Reload and use Save product.", 410));
    public Task<Result<ProductWorkspace>> SaveVariantAsync(int productId, WorkspaceVariant command) =>
        Task.FromResult<Result<ProductWorkspace>>(new Error("The product editor has been updated. Reload and use Save product.", 410));
    public Task<Result<ProductWorkspace>> AdjustStockAsync(int productId, WorkspaceStock command) =>
        Task.FromResult<Result<ProductWorkspace>>(new Error("The product editor has been updated. Reload and use Save product.", 410));
    public Task<Result<CombinationPreview>> PreviewAsync(int productId, long revision) =>
        Task.FromResult<Result<CombinationPreview>>(new Error("The product editor has been updated. Reload and use Save product.", 410));
    public Task<Result<ProductWorkspace>> GenerateAsync(int productId, long revision) =>
        Task.FromResult<Result<ProductWorkspace>>(new Error("The product editor has been updated. Reload and use Save product.", 410));
}