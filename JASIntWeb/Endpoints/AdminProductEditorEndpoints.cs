using JASIntWeb.Client.Contracts;
using JASIntWeb.Helpers;
namespace JASIntWeb.Endpoints;

public static class AdminProductEditorEndpoints
{
    extension (IEndpointRouteBuilder endpoints)
    {
        public void MapAdminProductEditorEndpoints()
        {
            var group = endpoints.MapGroup("/api/admin/product-editor").RequireAuthorization("AdminOnly").AddEndpointFilter<AdminCatalogFilter>();
            group.MapGet("/templates/{id:int}", async (int id, IAdminProductEditorBackend b, CancellationToken ct) => (await b.TemplateAsync(id, ct)).ToHttpResult());
            group.MapPut("/templates/{id:int}", async (int id, EditorTemplateData c, IAdminProductEditorBackend b, CancellationToken ct) => id != c.Id ? Results.BadRequest(new AdminCatalogError("Template IDs do not match.")) : (await b.SaveTemplateAsync(c, ct)).ToHttpResult());
            group.MapGet("/suggestions/{id:int}", async (int id, IAdminProductEditorBackend b, CancellationToken ct) => (await b.SuggestionAsync(id, ct)).ToHttpResult());
            group.MapPut("/suggestions/{id:int}", async (int id, EditorSuggestionData c, IAdminProductEditorBackend b, CancellationToken ct) => id != c.Id ? Results.BadRequest(new AdminCatalogError("Suggestion IDs do not match.")) : (await b.SaveSuggestionAsync(c, ct)).ToHttpResult());
            group.MapGet("/{id:int}", async (int id, IAdminProductEditorBackend backend, CancellationToken ct) => (await backend.LoadAsync(id, ct)).ToHttpResult());
            group.MapPost("/preview", async (EditorCombinationQuery command, IAdminProductEditorBackend backend, CancellationToken ct) => (await backend.PreviewAsync(command, ct)).ToHttpResult());
            group.MapPost("", async (EditorSave command, IAdminProductEditorBackend backend, CancellationToken ct) =>
            {
                if (command.Draft?.Id != 0) return Results.BadRequest(new AdminCatalogError("New products cannot specify an existing ID."));
                return (await backend.SaveAsync(command, ct)).ToHttpResult();
            });
            group.MapPut("/{id:int}", async (int id, EditorSave command, IAdminProductEditorBackend backend, CancellationToken ct) =>
            {
                if (id <= 0 || command.Draft?.Id != id) return Results.BadRequest(new AdminCatalogError("Product IDs do not match."));
                return (await backend.SaveAsync(command, ct)).ToHttpResult();
            });
            group.MapPost("/{id:int}/stock", async (int id, EditorStock command, IAdminProductEditorBackend backend, CancellationToken ct) =>
            {
                if (id <= 0 || id != command.ProductId) return Results.BadRequest(new AdminCatalogError("Product IDs do not match."));
                return (await backend.AdjustStockAsync(command, ct)).ToHttpResult();
            });
            group.MapGet("/operations/{operation:guid}", async (Guid operation, IAdminProductEditorBackend backend, CancellationToken ct) =>
            {
                var result = await backend.OperationAsync(operation, ct);
                return result switch
                {
                    EditorCommit commit => Results.Ok(commit),
                    null => Results.NoContent(),
                    Error err => err.ToHttpResult()
                };
            });
        }
    }
}
