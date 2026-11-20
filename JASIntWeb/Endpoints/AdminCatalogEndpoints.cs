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
        workspace.MapMethods("/{action}", ["POST", "PUT"], () => EditorUpdated());
        group.MapGet("/categories/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.CategoryAsync(id));
        group.MapGet("/brands/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.BrandAsync(id));
        group.MapGet("/products/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.ProductAsync(id));
        group.MapGet("/guides/editor/{id:int}", (int id, IAdminCatalogBackend backend) => backend.GuideAsync(id));
        group.MapPost("/categories", (CategoryEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveCategoryAsync(input)));
        group.MapPost("/brands", (BrandEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveBrandAsync(input)));
        group.MapPost("/guides", (GuideEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveGuideAsync(input)));
        group.MapPost("/products", () => EditorUpdated());
        group.MapPut("/categories/{id:int}", (int id, CategoryEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveCategoryAsync(input)));
        group.MapPut("/brands/{id:int}", (int id, BrandEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveBrandAsync(input)));
        group.MapPut("/guides/{id:int}", (int id, GuideEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveGuideAsync(input)));
        group.MapPut("/products/{id:int}", () => EditorUpdated());
        group.MapDelete("/brands/{id:int}", async (int id, IAdminCatalogBackend backend) => { await backend.DeleteBrandAsync(id); return new AdminSaveResult(id, "/admin/brands"); });
        group.MapDelete("/guides/{id:int}", async (int id, IAdminCatalogBackend backend) => { await backend.DeleteGuideAsync(id); return new AdminSaveResult(id, "/admin/guides"); });
    }
    private static IResult EditorUpdated() => Results.Json(new AdminCatalogError("The product editor has been updated. Reload and use Save product in /admin/products/{id}/edit."), statusCode: 410);
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
