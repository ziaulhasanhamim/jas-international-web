using JASIntWeb.Client.Contracts;
namespace JASIntWeb.Endpoints;

public static class AdminProductEditorEndpoints
{
    public static void MapAdminProductEditorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/product-editor").RequireAuthorization("AdminOnly").AddEndpointFilter<AdminCatalogFilter>();
        group.MapGet("/templates/{id:int}", (int id, IAdminProductEditorBackend b, CancellationToken ct) => b.TemplateAsync(id, ct));
        group.MapPut("/templates/{id:int}", (int id, EditorTemplateData c, IAdminProductEditorBackend b, CancellationToken ct) => id == c.Id ? b.SaveTemplateAsync(c, ct) : throw new AdminCatalogException(400, "Template IDs do not match."));
        group.MapGet("/suggestions/{id:int}", (int id, IAdminProductEditorBackend b, CancellationToken ct) => b.SuggestionAsync(id, ct));
        group.MapPut("/suggestions/{id:int}", (int id, EditorSuggestionData c, IAdminProductEditorBackend b, CancellationToken ct) => id == c.Id ? b.SaveSuggestionAsync(c, ct) : throw new AdminCatalogException(400, "Suggestion IDs do not match."));
        group.MapGet("/{id:int}", (int id, IAdminProductEditorBackend backend, CancellationToken ct) => backend.LoadAsync(id, ct));
        group.MapPost("/preview", (EditorCombinationQuery command, IAdminProductEditorBackend backend, CancellationToken ct) => backend.PreviewAsync(command, ct));
        group.MapPost("", (EditorSave command, IAdminProductEditorBackend backend, CancellationToken ct) =>
        {
            if (command.Draft?.Id != 0) throw new AdminCatalogException(400, "New products cannot specify an existing ID.");
            return backend.SaveAsync(command, ct);
        });
        group.MapPut("/{id:int}", (int id, EditorSave command, IAdminProductEditorBackend backend, CancellationToken ct) =>
        {
            if (id <= 0 || command.Draft?.Id != id) throw new AdminCatalogException(400, "Product IDs do not match.");
            return backend.SaveAsync(command, ct);
        });
        group.MapPost("/{id:int}/stock", (int id, EditorStock command, IAdminProductEditorBackend backend, CancellationToken ct) =>
        {
            if (id <= 0 || id != command.ProductId) throw new AdminCatalogException(400, "Product IDs do not match.");
            return backend.AdjustStockAsync(command, ct);
        });
        group.MapGet("/operations/{operation:guid}", async (Guid operation, IAdminProductEditorBackend backend, CancellationToken ct) =>
        {
            var result = await backend.OperationAsync(operation, ct);
            return result is null ? Results.NoContent() : Results.Ok(result);
        });
    }
}
