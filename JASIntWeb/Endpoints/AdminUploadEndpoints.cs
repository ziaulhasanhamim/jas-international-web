using JASIntWeb.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JASIntWeb.Endpoints;

public static class AdminUploadEndpoints
{
    private const int Megabyte = 1024 * 1024;
    private static readonly HashSet<string> ImageExtensions =
        [with(StringComparer.OrdinalIgnoreCase), ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif", ".svg"];

    public static void MapAdminUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var uploads = endpoints.MapGroup("/api/admin/uploads").RequireAuthorization("AdminOnly");
        uploads.MapGet("/security", Security);
        uploads.MapPost("/product-images", UploadProductImages);
        uploads.MapPost("/brand-logo", UploadBrandLogo);
        uploads.MapPost("/image", UploadImage);
    }

    private static Ok<UploadSecurityResponse> Security(HttpContext context, IAntiforgery antiforgery)
    {
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new UploadSecurityResponse(antiforgery.GetAndStoreTokens(context).RequestToken));
    }

    private static async Task<Results<Ok<UploadedImagesResponse>, BadRequest<EndpointError>>> UploadProductImages(
        HttpContext context, IStorageService storage, IAntiforgery antiforgery, ILoggerFactory logs, CancellationToken ct)
    {
        return (await FormRequestReader.ReadAsync(context, antiforgery, ct)) switch
        {
            IFormCollection form => await StoreProductImagesAsync(form, storage, logs, ct),
            EndpointError error => TypedResults.BadRequest(error)
        };
    }

    private static async Task<Results<Ok<UploadedImagesResponse>, BadRequest<EndpointError>>> StoreProductImagesAsync(
        IFormCollection form, IStorageService storage, ILoggerFactory logs, CancellationToken ct)
    {
        if (form.Files.Count is 0 or > 10)
            return TypedResults.BadRequest(new EndpointError("Upload between 1 and 10 images."));

        // Validate the entire batch before writing anything to storage.
        foreach (var file in form.Files)
        {
            if (ValidateFile(file, 10 * Megabyte) is { } message)
                return TypedResults.BadRequest(new EndpointError(message));
        }

        try
        {
            List<string> urls = [with(capacity: form.Files.Count)];
            foreach (var file in form.Files) urls.Add(await StoreAsync(file, storage, ct));
            return TypedResults.Ok(new UploadedImagesResponse(urls));
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logs.CreateLogger(nameof(AdminUploadEndpoints)).LogWarning(exception, "Product image upload failed.");
            return TypedResults.BadRequest(UploadError());
        }
    }

    private static Task<Results<Ok<UploadedImageResponse>, BadRequest<EndpointError>>> UploadBrandLogo(
        HttpContext context, IStorageService storage, IAntiforgery antiforgery, ILoggerFactory logs, CancellationToken ct) =>
        UploadSingleAsync(context, storage, antiforgery, logs, 5 * Megabyte, ct);

    private static Task<Results<Ok<UploadedImageResponse>, BadRequest<EndpointError>>> UploadImage(
        HttpContext context, IStorageService storage, IAntiforgery antiforgery, ILoggerFactory logs, CancellationToken ct) =>
        UploadSingleAsync(context, storage, antiforgery, logs, 10 * Megabyte, ct);

    private static async Task<Results<Ok<UploadedImageResponse>, BadRequest<EndpointError>>> UploadSingleAsync(
        HttpContext context, IStorageService storage, IAntiforgery antiforgery, ILoggerFactory logs, int limit, CancellationToken ct)
    {
        return (await FormRequestReader.ReadAsync(context, antiforgery, ct)) switch
        {
            IFormCollection form => await StoreSingleAsync(form, storage, logs, limit, ct),
            EndpointError error => TypedResults.BadRequest(error)
        };
    }

    private static async Task<Results<Ok<UploadedImageResponse>, BadRequest<EndpointError>>> StoreSingleAsync(
        IFormCollection form, IStorageService storage, ILoggerFactory logs, int limit, CancellationToken ct)
    {
        if (form.Files.FirstOrDefault() is not { } file)
            return TypedResults.BadRequest(new EndpointError("No file uploaded"));
        if (ValidateFile(file, limit) is { } message)
            return TypedResults.BadRequest(new EndpointError(message));

        try
        {
            return TypedResults.Ok(new UploadedImageResponse(await StoreAsync(file, storage, ct)));
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logs.CreateLogger(nameof(AdminUploadEndpoints)).LogWarning(exception, "Image upload failed.");
            return TypedResults.BadRequest(UploadError());
        }
    }

    private static string? ValidateFile(IFormFile file, int limit) => file.Length switch
    {
        0 => "No file uploaded",
        _ when file.Length > limit => $"Image file exceeds {limit / Megabyte}MB limit",
        _ when !ImageExtensions.Contains(Path.GetExtension(file.FileName)) => "Unsupported image format.",
        _ => null
    };

    private static EndpointError UploadError() => new("The image could not be uploaded. Check its format and try again.");

    private static async Task<string> StoreAsync(IFormFile file, IStorageService storage, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await ImageUploadValidator.UploadAsync(storage, stream, file.FileName, file.ContentType, ct);
    }
}
