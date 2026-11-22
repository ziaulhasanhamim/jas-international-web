using JASIntWeb.Client.Contracts;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Services;

public sealed class HttpAdminProductWorkspaceBackend(HttpAdminCatalogBackend http) : IAdminProductWorkspaceBackend
{
    private static string Path(int id, string suffix = "") => $"api/admin/products/{id}/workspace{suffix}";
    public Task<Result<ProductWorkspace>> LoadAsync(int productId) => http.Send<ProductWorkspace>(HttpMethod.Get, Path(productId));
    public Task<Result<ProductWorkspace>> SaveConfigurationAsync(int productId, ProductConfiguration command) => http.Send<ProductWorkspace>(HttpMethod.Put, Path(productId, "/configuration"), command);
    public Task<Result<ProductWorkspace>> SaveVariantAsync(int productId, WorkspaceVariant command) => http.Send<ProductWorkspace>(HttpMethod.Put, Path(productId, "/variant"), command);
    public Task<Result<ProductWorkspace>> AdjustStockAsync(int productId, WorkspaceStock command) => http.Send<ProductWorkspace>(HttpMethod.Post, Path(productId, "/stock"), command);
    public Task<Result<CombinationPreview>> PreviewAsync(int productId, long revision) => http.Send<CombinationPreview>(HttpMethod.Post, Path(productId, "/preview"), new WorkspaceRevision(revision));
    public Task<Result<ProductWorkspace>> GenerateAsync(int productId, long revision) => http.Send<ProductWorkspace>(HttpMethod.Post, Path(productId, "/generate"), new WorkspaceRevision(revision));
}
