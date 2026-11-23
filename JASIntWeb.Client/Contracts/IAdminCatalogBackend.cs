using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;

namespace JASIntWeb.Client.Contracts;

public sealed record AdminEditorData<T>(T Input, List<CategoryLinkDto> Categories, List<BrandLinkDto> Brands,
    int ChildCount, int ProductCount, string PublicBaseUrl);
public sealed record AdminSaveResult(int Id, string RedirectUrl);
public sealed record AdminCatalogError(string Message, Dictionary<string, string[]>? Errors = null);
public sealed record ContentPreviewRequest(string Source);
public sealed record ContentPreview(string Html);

public interface IAdminCatalogBackend
{
    Task<Result<AdminEditorData<CategoryEditForm>>> CategoryAsync(int id);
    Task<Result<AdminEditorData<BrandEditForm>>> BrandAsync(int id);
    Task<Result<AdminEditorData<ProductEditForm>>> ProductAsync(int id);
    Task<Result<AdminEditorData<GuideEditForm>>> GuideAsync(int id);
    Task<Result<AdminSaveResult>> SaveCategoryAsync(CategoryEditForm input);
    Task<Result<AdminSaveResult>> SaveBrandAsync(BrandEditForm input);
    Task<Result<AdminSaveResult>> SaveProductAsync(ProductEditForm input);
    Task<Result<AdminSaveResult>> SaveGuideAsync(GuideEditForm input);
    Task<Result<ContentPreview>> PreviewContentAsync(string source);
    Task<Result> DeleteBrandAsync(int id);
    Task<Result> DeleteGuideAsync(int id);
    Task<Result<List<TagDto>>> TagsAsync();
    Task<Result<AdminSaveResult>> SaveTagAsync(TagFormModel input);
    Task<Result> DeleteTagAsync(int id);
    Task<Result<string>> UploadAsync(Stream stream, string fileName, string contentType, bool brandLogo);
}
