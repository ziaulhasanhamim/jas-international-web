namespace JASIntWeb.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCartEndpoints();
        endpoints.MapOrderEndpoints();
        endpoints.MapHealthEndpoints();
        endpoints.MapSeoEndpoints();
        endpoints.MapMetaFeedEndpoints();
        endpoints.MapAnalyticsEndpoints();
        endpoints.MapAdminUploadEndpoints();
        endpoints.MapPaymentWebhookEndpoints();
        endpoints.MapAccountEndpoints();
        endpoints.MapCustomerConfidenceEndpoints();
        return endpoints;
    }
}
