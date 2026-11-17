using JASIntWeb.Endpoints;
using JASIntWeb.Middleware;

namespace JASIntWeb.Services;

internal static class ApplicationPipeline
{
    public static void UseStorePipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseWebSockets();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }
        app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
            branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseHttpsRedirection();
        app.UseMiddleware<CanonicalHostAndRedirectMiddleware>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(JASIntWeb.Services.OrderAccess.ProtectAsync);
        app.UseOutputCache();
        app.UseMiddleware<MetaAttributionMiddleware>();
        app.UseMiddleware<CartRequestMiddleware>();
        app.UseAntiforgery();

        var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
            RequestPath = "/uploads"
        });
        app.UseStaticFiles();
        app.MapStaticAssets();
    }
}
