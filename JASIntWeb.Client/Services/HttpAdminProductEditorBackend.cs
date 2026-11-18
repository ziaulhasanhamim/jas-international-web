using JASIntWeb.Client.Contracts;
using System.Net.Http.Json;
namespace JASIntWeb.Client.Services;

public sealed class HttpAdminProductEditorBackend(HttpAdminCatalogBackend http, IHttpClientFactory clients) : IAdminProductEditorBackend
{
    private const string Root = "api/admin/product-editor";
    public Task<EditorTemplateData> TemplateAsync(int id, CancellationToken ct = default) => http.Send<EditorTemplateData>(HttpMethod.Get, $"{Root}/templates/{id}", ct: ct);
    public Task<EditorTemplateData> SaveTemplateAsync(EditorTemplateData command, CancellationToken ct = default) => http.Send<EditorTemplateData>(HttpMethod.Put, $"{Root}/templates/{command.Id}", command, ct: ct);
    public Task<EditorSuggestionData> SuggestionAsync(int id, CancellationToken ct = default) => http.Send<EditorSuggestionData>(HttpMethod.Get, $"{Root}/suggestions/{id}", ct: ct);
    public Task<EditorSuggestionData> SaveSuggestionAsync(EditorSuggestionData command, CancellationToken ct = default) => http.Send<EditorSuggestionData>(HttpMethod.Put, $"{Root}/suggestions/{command.Id}", command, ct: ct);
    public Task<ProductEditorData> LoadAsync(int id, CancellationToken ct = default) => http.Send<ProductEditorData>(HttpMethod.Get, $"{Root}/{id}", ct: ct);
    public Task<EditorCombinationPage> PreviewAsync(EditorCombinationQuery query, CancellationToken ct = default) => http.Send<EditorCombinationPage>(HttpMethod.Post, $"{Root}/preview", query, ct: ct);
    public Task<EditorCommit> SaveAsync(EditorSave command, CancellationToken ct = default) => http.Send<EditorCommit>(command.Draft.Id == 0 ? HttpMethod.Post : HttpMethod.Put, command.Draft.Id == 0 ? Root : $"{Root}/{command.Draft.Id}", command, ct: ct);
    public Task<EditorCommit> AdjustStockAsync(EditorStock command, CancellationToken ct = default) => http.Send<EditorCommit>(HttpMethod.Post, $"{Root}/{command.ProductId}/stock", command, ct: ct);
    public async Task<EditorCommit?> OperationAsync(Guid operation, CancellationToken ct = default)
    {
        using var client = clients.CreateClient("Shop");
        using var response = await client.GetAsync($"{Root}/operations/{operation}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (!response.IsSuccessStatusCode) throw new AdminCatalogException((int)response.StatusCode, "The operation status could not be checked. Your draft is retained.");
        return await response.Content.ReadFromJsonAsync<EditorCommit>(ct);
    }
}
