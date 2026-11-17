using JASIntWeb.Client.Contracts;

namespace JASIntWeb.Client.Services;

public sealed class HttpAdminProductWorkspaceBackend(HttpAdminCatalogBackend http) : IAdminProductWorkspaceBackend
{
    private static string Path(int id, string suffix = "") => $"api/admin/products/{id}/workspace{suffix}";
    public Task<ProductWorkspace> LoadAsync(int productId) => http.Send<ProductWorkspace>(HttpMethod.Get, Path(productId));
    public Task<ProductWorkspace> SaveConfigurationAsync(int productId, ProductConfiguration command) => http.Send<ProductWorkspace>(HttpMethod.Put, Path(productId, "/configuration"), command);
    public Task<ProductWorkspace> SaveVariantAsync(int productId, WorkspaceVariant command) => http.Send<ProductWorkspace>(HttpMethod.Put, Path(productId, "/variant"), command);
    public Task<ProductWorkspace> AdjustStockAsync(int productId, WorkspaceStock command) => http.Send<ProductWorkspace>(HttpMethod.Post, Path(productId, "/stock"), command);
    public Task<CombinationPreview> PreviewAsync(int productId, long revision) => http.Send<CombinationPreview>(HttpMethod.Post, Path(productId, "/preview"), new WorkspaceRevision(revision));
    public Task<ProductWorkspace> GenerateAsync(int productId, long revision) => http.Send<ProductWorkspace>(HttpMethod.Post, Path(productId, "/generate"), new WorkspaceRevision(revision));
}
