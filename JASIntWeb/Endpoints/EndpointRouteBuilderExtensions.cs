namespace JASIntWeb.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCartEndpoints();
        endpoints.MapHealthEndpoints();
        return endpoints;
    }
}
