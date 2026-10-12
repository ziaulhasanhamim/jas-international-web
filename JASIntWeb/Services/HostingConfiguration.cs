using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
namespace JASIntWeb.Services;

internal static class HostingConfiguration
{
    public static void AddStoreHosting(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                foreach (var proxy in builder.Configuration.GetSection("Hosting:KnownProxies").Get<string[]>() ?? [])
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }
            });
        var keys = builder.Services.AddDataProtection().SetApplicationName("JASInternational");
        var keyPath = builder.Configuration["Hosting:DataProtectionPath"];
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            Directory.CreateDirectory(keyPath);
            keys.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }
        builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    {
                        var path = context.Request.Path;
                        if (!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsPut(context.Request.Method))
                        {
                            return RateLimitPartition.GetNoLimiter("read");
                        }
                        var account = path.StartsWithSegments("/account");
                        var checkout = path.StartsWithSegments("/checkout");
                        var cart = path.StartsWithSegments("/api/cart");
                        if (!account && !checkout && !cart)
                        {
                            return RateLimitPartition.GetNoLimiter("other");
                        }
                        var group = account ? "account" : checkout ? "checkout" : "cart";
                        var key = group + ":" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = account ? 10 : checkout ? 20 : 80,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    });
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    context.HttpContext.Response.Headers.CacheControl = "no-store";
                    await context.HttpContext.Response.WriteAsync("Too many attempts. Please wait one minute and try again.", cancellationToken);
                };
            });
    }
}
