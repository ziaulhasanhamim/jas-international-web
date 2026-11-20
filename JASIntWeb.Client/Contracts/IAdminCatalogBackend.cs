using JASIntWeb.Client.Dtos;
namespace JASIntWeb.Client.Contracts;

public sealed record AdminEditorData<T>(T Input, List<CategoryLinkDto> Categories, List<BrandLinkDto> Brands,
    int ChildCount, int ProductCount, string PublicBaseUrl);
public sealed record AdminSaveResult(int Id, string RedirectUrl);
public sealed record AdminCatalogError(string Message, Dictionary<string, string[]>? Errors = null);
public sealed class AdminCatalogException(int status, string message, Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}
public interface IAdminCatalogBackend
{
    Task<AdminEditorData<CategoryEditForm>> CategoryAsync(int id);
    Task<AdminEditorData<BrandEditForm>> BrandAsync(int id);
    Task<AdminEditorData<ProductEditForm>> ProductAsync(int id);
    Task<AdminEditorData<GuideEditForm>> GuideAsync(int id);
    Task<AdminSaveResult> SaveCategoryAsync(CategoryEditForm input);
    Task<AdminSaveResult> SaveBrandAsync(BrandEditForm input);
    Task<AdminSaveResult> SaveProductAsync(ProductEditForm input);
    Task<AdminSaveResult> SaveGuideAsync(GuideEditForm input);
    Task<ContentPreview> PreviewContentAsync(string source);
    Task DeleteBrandAsync(int id);
    Task DeleteGuideAsync(int id);
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, bool brandLogo);
}
