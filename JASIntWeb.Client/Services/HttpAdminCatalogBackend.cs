using System.Net.Http.Json;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Services;

public sealed class HttpAdminCatalogBackend(IHttpClientFactory factory) : IAdminCatalogBackend
{
    public async Task<Result<T>> Send<T>(HttpMethod method, string path, object? value = null, HttpContent? content = null, CancellationToken ct = default)
    {
        using var client = factory.CreateClient("Shop");
        using var request = new HttpRequestMessage(method, path);
        request.Content = content ?? (value is null ? null : JsonContent.Create(value, value.GetType()));
        if (method != HttpMethod.Get)
        {
            using var secured = await client.GetAsync("api/admin/uploads/security", ct);
            if (!secured.IsSuccessStatusCode) return new CatalogError("Your administrator session expired. Sign in again.", (int)secured.StatusCode);
            var security = await secured.Content.ReadFromJsonAsync<AntiforgeryDto>(ct);
            if (security is null || string.IsNullOrWhiteSpace(security.Token)) return new CatalogError("Your session expired. Sign in again.", 401);
            request.Headers.Add("X-CSRF-TOKEN", security.Token);
        }
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            AdminCatalogError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<AdminCatalogError>(ct); } catch (System.Text.Json.JsonException) { }
            return new CatalogError(error?.Message ?? "The request failed. Your changes have not been discarded.", (int)response.StatusCode, error?.Errors);
        }
        var result = await response.Content.ReadFromJsonAsync<T>(ct);
        return result is null ? new CatalogError("The server returned an empty response.", 500) : result;
    }

    public Task<Result<AdminEditorData<CategoryEditForm>>> CategoryAsync(int id) => Send<AdminEditorData<CategoryEditForm>>(HttpMethod.Get, $"api/admin/categories/editor/{id}");
    public Task<Result<AdminEditorData<BrandEditForm>>> BrandAsync(int id) => Send<AdminEditorData<BrandEditForm>>(HttpMethod.Get, $"api/admin/brands/editor/{id}");
    public Task<Result<AdminEditorData<ProductEditForm>>> ProductAsync(int id) => Send<AdminEditorData<ProductEditForm>>(HttpMethod.Get, $"api/admin/products/editor/{id}");
    public Task<Result<AdminEditorData<GuideEditForm>>> GuideAsync(int id) => Send<AdminEditorData<GuideEditForm>>(HttpMethod.Get, $"api/admin/guides/editor/{id}");
    private Task<Result<AdminSaveResult>> Save<T>(string kind, int id, T input) => Send<AdminSaveResult>(id == 0 ? HttpMethod.Post : HttpMethod.Put, $"api/admin/{kind}" + (id == 0 ? "" : $"/{id}"), input);
    public Task<Result<AdminSaveResult>> SaveCategoryAsync(CategoryEditForm input) => Save("categories", input.Id, input);
    public Task<Result<AdminSaveResult>> SaveBrandAsync(BrandEditForm input) => Save("brands", input.Id, input);
    public Task<Result<AdminSaveResult>> SaveProductAsync(ProductEditForm input) => Save("products", input.Id, input);
    public Task<Result<AdminSaveResult>> SaveGuideAsync(GuideEditForm input) => Save("guides", input.Id, input);
    public async Task<Result> DeleteBrandAsync(int id)
    {
        var res = await Send<AdminSaveResult>(HttpMethod.Delete, $"api/admin/brands/{id}");
        return res switch
        {
            AdminSaveResult => Result.Success,
            Error err => err
        };
    }
    public async Task<Result> DeleteGuideAsync(int id)
    {
        var res = await Send<AdminSaveResult>(HttpMethod.Delete, $"api/admin/guides/{id}");
        return res switch
        {
            AdminSaveResult => Result.Success,
            Error err => err
        };
    }
    public async Task<Result<string>> UploadAsync(Stream stream, string fileName, string contentType, bool brandLogo)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        var res = await Send<UploadedImage>(HttpMethod.Post, brandLogo ? "api/admin/uploads/brand-logo" : "api/admin/uploads/image", content: form);
        return res switch
        {
            UploadedImage img => img.Url,
            Error err => err
        };
    }
    public Task<Result<ContentPreview>> PreviewContentAsync(string source) => Send<ContentPreview>(HttpMethod.Post, "api/admin/content/preview", new ContentPreviewRequest(source));
    private sealed record UploadedImage(string Url);
}
