using JASIntWeb.Services;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using Microsoft.AspNetCore.Antiforgery;
namespace JASIntWeb.Endpoints;

public static class AdminCatalogEndpoints
{
    public static void MapAdminCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").RequireAuthorization("AdminOnly").AddEndpointFilter<AdminCatalogFilter>();
        group.MapPost("/content/preview", (ContentPreviewRequest input, IAdminCatalogBackend backend) => backend.PreviewContentAsync(input.Source));
        var workspace = group.MapGroup("/products/{id:int}/workspace");
        workspace.MapGet("", (int id, IAdminProductWorkspaceBackend backend) => backend.LoadAsync(id));
        workspace.MapPut("/configuration", (int id, ProductConfiguration command, IAdminProductWorkspaceBackend backend) => backend.SaveConfigurationAsync(id, command));
        workspace.MapPut("/variant", (int id, WorkspaceVariant command, IAdminProductWorkspaceBackend backend) => backend.SaveVariantAsync(id, command));
        workspace.MapPost("/stock", (int id, WorkspaceStock command, IAdminProductWorkspaceBackend backend) => backend.AdjustStockAsync(id, command));
        workspace.MapPost("/preview", (int id, WorkspaceRevision command, IAdminProductWorkspaceBackend backend) => backend.PreviewAsync(id, command.Revision));
        workspace.MapPost("/generate", (int id, WorkspaceRevision command, IAdminProductWorkspaceBackend backend) => backend.GenerateAsync(id, command.Revision));
        group.MapGet("/categories/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.CategoryAsync(id));
        group.MapGet("/brands/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.BrandAsync(id));
        group.MapGet("/products/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.ProductAsync(id));
        group.MapPost("/categories", (CategoryEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveCategoryAsync(input)));
        group.MapPost("/brands", (BrandEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveBrandAsync(input)));
        group.MapPost("/products", (ProductEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveProductAsync(input)));
        group.MapPut("/categories/{id:int}", (int id, CategoryEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveCategoryAsync(input)));
        group.MapPut("/brands/{id:int}", (int id, BrandEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveBrandAsync(input)));
        group.MapPut("/products/{id:int}", (int id, ProductEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveProductAsync(input)));
        group.MapDelete("/brands/{id:int}", async (int id, IAdminCatalogBackend backend) => { await backend.DeleteBrandAsync(id); return new AdminSaveResult(id, "/admin/brands"); });
    }
    private static async Task<IResult> Create(int id, Func<Task<AdminSaveResult>> save)
    {
        if (id != 0) throw new AdminCatalogException(400, "New records must not specify an existing ID.");
        var result = await save();
        return Results.Created(result.RedirectUrl, result);
    }
    private static async Task<IResult> Update(int routeId, int bodyId, Func<Task<AdminSaveResult>> save)
    {
        if (routeId <= 0 || routeId != bodyId) throw new AdminCatalogException(400, "Record IDs do not match.");
        return Results.Ok(await save());
    }
}

public sealed class AdminCatalogFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try
        {
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method)) await antiforgery.ValidateRequestAsync(context.HttpContext);
            return await next(context);
        }
        catch (AntiforgeryValidationException) { return Results.BadRequest(new AdminCatalogError("Refresh the page and try again.")); }
        catch (ShopException error) { return Results.Json(new AdminCatalogError(error.Message), statusCode: error.Message.Contains("session expired", StringComparison.OrdinalIgnoreCase) ? 401 : 409); }
        catch (AdminCatalogException error) { return Results.Json(new AdminCatalogError(error.Message, error.Errors), statusCode: error.Status); }
    }
}
