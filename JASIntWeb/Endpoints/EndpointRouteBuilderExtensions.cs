namespace JASIntWeb.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    extension (IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapStoreEndpoints()
        {
            endpoints.MapAdminCatalogEndpoints();
            endpoints.MapAdminProductEditorEndpoints();
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
}
