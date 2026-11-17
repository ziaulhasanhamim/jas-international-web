using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
namespace JASIntWeb.Client.Services;

public sealed class HttpAdminCatalogBackend(IHttpClientFactory factory) : IAdminCatalogBackend
{
    private async Task<T> Send<T>(HttpMethod method, string path, object? value = null, HttpContent? content = null)
    {
        using var client = factory.CreateClient("Shop");
        using var request = new HttpRequestMessage(method, path);
        request.Content = content ?? (value is null ? null : JsonContent.Create(value, value.GetType()));
        if (method != HttpMethod.Get)
        {
            using var secured = await client.GetAsync("api/admin/uploads/security");
            if (!secured.IsSuccessStatusCode) throw new AdminCatalogException((int)secured.StatusCode, "Your administrator session expired. Sign in again.");
            var security = await secured.Content.ReadFromJsonAsync<AntiforgeryDto>()
                ?? throw new AdminCatalogException(401, "Your session expired. Sign in again.");
            request.Headers.Add("X-CSRF-TOKEN", security.Token);
        }
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            AdminCatalogError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<AdminCatalogError>(); } catch (System.Text.Json.JsonException) { }
            throw new AdminCatalogException((int)response.StatusCode, error?.Message ?? "The request failed. Your changes have not been discarded.", error?.Errors);
        }
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new AdminCatalogException(500, "The server returned an empty response.");
    }
    public Task<AdminEditorData<CategoryEditForm>> CategoryAsync(int id) => Send<AdminEditorData<CategoryEditForm>>(HttpMethod.Get, $"api/admin/categories/editor/{id}");
    public Task<AdminEditorData<BrandEditForm>> BrandAsync(int id) => Send<AdminEditorData<BrandEditForm>>(HttpMethod.Get, $"api/admin/brands/editor/{id}");
    public Task<AdminEditorData<ProductEditForm>> ProductAsync(int id) => Send<AdminEditorData<ProductEditForm>>(HttpMethod.Get, $"api/admin/products/editor/{id}");
    private Task<AdminSaveResult> Save<T>(string kind, int id, T input) => Send<AdminSaveResult>(id == 0 ? HttpMethod.Post : HttpMethod.Put, $"api/admin/{kind}" + (id == 0 ? "" : $"/{id}"), input);
    public Task<AdminSaveResult> SaveCategoryAsync(CategoryEditForm input) => Save("categories", input.Id, input);
    public Task<AdminSaveResult> SaveBrandAsync(BrandEditForm input) => Save("brands", input.Id, input);
    public Task<AdminSaveResult> SaveProductAsync(ProductEditForm input) => Save("products", input.Id, input);
    public async Task DeleteBrandAsync(int id) => await Send<AdminSaveResult>(HttpMethod.Delete, $"api/admin/brands/{id}");
    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, bool brandLogo)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return (await Send<UploadedImage>(HttpMethod.Post, brandLogo ? "api/admin/uploads/brand-logo" : "api/admin/uploads/image", content: form)).Url;
    }
    private sealed record UploadedImage(string Url);
}
