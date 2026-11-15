using System.Collections.Concurrent;
using JASIntWeb.Persistence;
using JASIntWeb.Services.Seo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JASIntWeb.Middleware;

public sealed class CanonicalHostAndRedirectMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<string, string> MemoryRedirects = new(StringComparer.OrdinalIgnoreCase);
    private static DateTimeOffset _lastRedirectSync = DateTimeOffset.MinValue;

    public async Task InvokeAsync(HttpContext context, ISeoService seo, IServiceProvider sp, IWebHostEnvironment env)
    {
        var request = context.Request;
        var host = request.Host.Host;
        var path = request.Path.Value ?? "/";
        var query = request.QueryString.Value ?? string.Empty;

        // 0. Bypass /health checks so internal container probes (Render, AWS, k8s, Docker)
        // and external monitors can verify service availability without 301 redirection.
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // 1. Canonical Host & HTTPS Enforcer in non-development environments
        if (!env.IsDevelopment() && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            var targetHost = "www.jasinternational.com.bd";
            var isWrongHost = !host.Equals(targetHost, StringComparison.OrdinalIgnoreCase);
            var isWrongScheme = !request.IsHttps;

            if (isWrongHost || isWrongScheme)
            {
                context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                context.Response.Headers.Location = $"https://{targetHost}{path}{query}";
                return;
            }
        }

        // 2. Trailing slash normalization (except root '/')
        if (path.Length > 1 && path.EndsWith('/'))
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = path.TrimEnd('/') + query;
            return;
        }

        // 3. Brand query migration: /products?brand=xyz -> /brand/xyz
        if (path.Equals("/products", StringComparison.OrdinalIgnoreCase) &&
            request.Query.TryGetValue("brand", out var brandSlug) &&
            !string.IsNullOrWhiteSpace(brandSlug) &&
            !request.Query.ContainsKey("q"))
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = $"/brand/{brandSlug.ToString().Trim().ToLowerInvariant()}";
            return;
        }

        // 4. Legacy category route consolidation: /products/{slug} -> /category/{slug}
        if (path.StartsWith("/products/", StringComparison.OrdinalIgnoreCase))
        {
            var slug = path["/products/".Length..].Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(slug))
            {
                context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                context.Response.Headers.Location = $"/category/{slug}{query}";
                return;
            }
        }

        if (path.Equals("/category", StringComparison.OrdinalIgnoreCase) || path.Equals("/category/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = $"/products{query}";
            return;
        }

        // 5. Database 301 redirects (from UrlRedirect table, cached)
        if (DateTimeOffset.UtcNow - _lastRedirectSync > TimeSpan.FromMinutes(5))
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var redirects = await db.UrlRedirects.AsNoTracking().ToListAsync();
                MemoryRedirects.Clear();
                foreach (var r in redirects)
                {
                    MemoryRedirects[r.OldPath.Trim().ToLowerInvariant()] = r.NewPath.Trim();
                }
                _lastRedirectSync = DateTimeOffset.UtcNow;
            }
            catch
            {
                // Silently fallback to existing memory dictionary
            }
        }

        var normalizedLookup = path.Trim().ToLowerInvariant();
        if (MemoryRedirects.TryGetValue(normalizedLookup, out var targetRedirect))
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = targetRedirect + query;
            return;
        }

        await next(context);
    }
}
