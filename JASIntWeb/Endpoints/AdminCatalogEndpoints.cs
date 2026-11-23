using JASIntWeb.Services;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Dtos;
using JASIntWeb.Helpers;
using Microsoft.AspNetCore.Antiforgery;
namespace JASIntWeb.Endpoints;

public static class AdminCatalogEndpoints
{
    extension (IEndpointRouteBuilder endpoints)
    {
        public void MapAdminCatalogEndpoints()
        {
            var group = endpoints.MapGroup("/api/admin").RequireAuthorization("AdminOnly").AddEndpointFilter<AdminCatalogFilter>();
            group.MapPost("/content/preview", async (ContentPreviewRequest input, IAdminCatalogBackend backend) => (await backend.PreviewContentAsync(input.Source)).ToHttpResult());
            var workspace = group.MapGroup("/products/{id:int}/workspace");
            workspace.MapGet("", async (int id, IAdminProductWorkspaceBackend backend) => (await backend.LoadAsync(id)).ToHttpResult());
            workspace.MapMethods("/{action}", ["POST", "PUT"], () => EditorUpdated());
            group.MapGet("/categories/editor/{id:int}", async (int id, IAdminCatalogBackend backend) => (await backend.CategoryAsync(id)).ToHttpResult());
            group.MapGet("/brands/editor/{id:int}", async (int id, IAdminCatalogBackend backend) => (await backend.BrandAsync(id)).ToHttpResult());
            group.MapGet("/products/editor/{id:int}", async (int id, IAdminCatalogBackend backend) => (await backend.ProductAsync(id)).ToHttpResult());
            group.MapGet("/guides/editor/{id:int}", async (int id, IAdminCatalogBackend backend) => (await backend.GuideAsync(id)).ToHttpResult());
            group.MapPost("/categories", (CategoryEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveCategoryAsync(input)));
            group.MapPost("/brands", (BrandEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveBrandAsync(input)));
            group.MapPost("/guides", (GuideEditForm input, IAdminCatalogBackend backend) => Create(input.Id, () => backend.SaveGuideAsync(input)));
            group.MapPost("/products", () => EditorUpdated());
            group.MapPut("/categories/{id:int}", (int id, CategoryEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveCategoryAsync(input)));
            group.MapPut("/brands/{id:int}", (int id, BrandEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveBrandAsync(input)));
            group.MapPut("/guides/{id:int}", (int id, GuideEditForm input, IAdminCatalogBackend backend) => Update(id, input.Id, () => backend.SaveGuideAsync(input)));
            group.MapPut("/products/{id:int}", () => EditorUpdated());
            group.MapDelete("/brands/{id:int}", async (int id, IAdminCatalogBackend backend) =>
            {
                var res = await backend.DeleteBrandAsync(id);
                return res switch
                {
                    Success => Results.Ok(new AdminSaveResult(id, "/admin/brands")),
                    Error err => err.ToHttpResult()
                };
            });
            group.MapDelete("/guides/{id:int}", async (int id, IAdminCatalogBackend backend) =>
            {
                var res = await backend.DeleteGuideAsync(id);
                return res switch
                {
                    Success => Results.Ok(new AdminSaveResult(id, "/admin/guides")),
                    Error err => err.ToHttpResult()
                };
            });
            group.MapGet("/tags", async (IAdminCatalogBackend backend) => (await backend.TagsAsync()).ToHttpResult());
            group.MapPost("/tags", async (TagFormModel input, IAdminCatalogBackend backend) =>
            {
                var result = await backend.SaveTagAsync(input);
                return result switch
                {
                    AdminSaveResult res => Results.Ok(res),
                    Error err => err.ToHttpResult()
                };
            });
            group.MapDelete("/tags/{id:int}", async (int id, IAdminCatalogBackend backend) =>
            {
                var res = await backend.DeleteTagAsync(id);
                return res switch
                {
                    Success => Results.Ok(new AdminSaveResult(id, "/admin/tags")),
                    Error err => err.ToHttpResult()
                };
            });
        }
    }
    private static IResult EditorUpdated() => Results.Json(new AdminCatalogError("The product editor has been updated. Reload and use Save product in /admin/products/{id}/edit."), statusCode: 410);
    private static async Task<IResult> Create(int id, Func<Task<Result<AdminSaveResult>>> save)
    {
        if (id != 0) return Results.BadRequest(new AdminCatalogError("New records must not specify an existing ID."));
        var result = await save();
        return result switch
        {
            AdminSaveResult res => Results.Created(res.RedirectUrl, res),
            Error err => err.ToHttpResult()
        };
    }
    private static async Task<IResult> Update(int routeId, int bodyId, Func<Task<Result<AdminSaveResult>>> save)
    {
        if (routeId <= 0 || routeId != bodyId) return Results.BadRequest(new AdminCatalogError("Record IDs do not match."));
        var result = await save();
        return result switch
        {
            AdminSaveResult res => Results.Ok(res),
            Error err => err.ToHttpResult()
        };
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
    }
}
