using JASIntWeb.Client.Contracts;
using JASIntWeb.Client.Services;
using JASIntWeb.Components.Account;
using JASIntWeb.Services.Analytics;
using JASIntWeb.Services.Meta;
using JASIntWeb.Services.Seo;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

internal static class ServiceCollectionExtensions
{
    public static void AddStoreServices(this WebApplicationBuilder builder)
    {
        ConfigurePort(builder);
        builder.AddStoreHosting();
        ConfigureForwardedHeaders(builder);
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        AddPersistence(builder);
        AddCaching(builder);
        AddStorefront(builder);
        AddAccounts(builder);
        AddCommerce(builder);
    }

    private static void ConfigurePort(WebApplicationBuilder builder)
    {
        var portEnv = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrEmpty(portEnv))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");
        }
    }

    private static void ConfigureForwardedHeaders(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
            foreach (var proxy in builder.Configuration.GetSection("Hosting:KnownProxies").Get<string[]>() ?? [])
            {
                if (!System.Net.IPAddress.TryParse(proxy, out var address))
                    throw new InvalidOperationException("Hosting:KnownProxies must contain valid IP addresses.");
                options.KnownProxies.Add(address);
            }
            options.ForwardLimit = 1;
        });
    }

    private static void AddPersistence(WebApplicationBuilder builder)
    {
        // Built-in .NET Health Check with EF Core Database Connectivity Check
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "database",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready", "db"]);

        // Pooled DbContext Factory: pools DbContext instances to eliminate instantiation overhead and reduce GC allocations
        builder.Services.AddPooledDbContextFactory<AppDbContext>(o => o.UseNpgsql(DatabaseConfiguration.ResolvePostgresConnectionString(builder.Configuration)));
        builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
    }

    private static void AddCaching(WebApplicationBuilder builder)
    {
        builder.Services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(30)
            };
        });

        builder.Services.AddOutputCache(options =>
        {
            options.AddBasePolicy(b => b.With(c =>
                c.HttpContext.Request.Path.StartsWithSegments("/about")
                || c.HttpContext.Request.Path.StartsWithSegments("/faq")
                || c.HttpContext.Request.Path.StartsWithSegments("/terms")
                || c.HttpContext.Request.Path.StartsWithSegments("/privacy")
                || c.HttpContext.Request.Path.StartsWithSegments("/warranty-returns"))
                .Expire(TimeSpan.FromHours(24))
                .Tag("static-content"));

            options.AddPolicy("StaticPages", b =>
                b.Expire(TimeSpan.FromHours(24)).Tag("static-content"));

            options.AddPolicy("HomePage", b =>
                b.Expire(TimeSpan.FromMinutes(10))
                 .Tag("homepage")
                 .Tag("catalog")
                 .With(c => c.HttpContext.User?.Identity?.IsAuthenticated != true)
                 .With(c => string.IsNullOrEmpty(c.HttpContext.Request.Headers.Authorization))
                 .With(c => !c.HttpContext.Request.Cookies.ContainsKey(".AspNetCore.Identity.Application")
                         && !c.HttpContext.Request.Cookies.ContainsKey(".AspNetCore.Identity.External")));

            options.AddPolicy("SitemapPolicy", b =>
                b.Expire(TimeSpan.FromHours(12)).Tag("seo"));
        });
    }

    private static void AddStorefront(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<CartService>();
        builder.Services.AddScoped<WishlistService>();
        builder.Services.AddScoped<ICartBackend, ServerCartBackend>();
        builder.Services.AddQuickGridEntityFrameworkAdapter();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = builder.Environment.IsDevelopment();
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
            })
            .AddHubOptions(options =>
            {
                options.MaximumReceiveMessageSize = 1024 * 1024;
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            })
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, JASIntWeb.Components.Account.IdentityRevalidatingAuthenticationStateProvider>();
    }

    private static void AddAccounts(WebApplicationBuilder builder)
    {
        var authBuilder = builder.Services.AddAuthentication(o =>
            {
                o.DefaultScheme = IdentityConstants.ApplicationScheme;
                o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            });
        authBuilder.AddIdentityCookies();

        var googleClientId = builder.Configuration["Authentication:Google:ClientId"]
            ?? builder.Configuration["GOOGLE_CLIENT_ID"];
        var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
            ?? builder.Configuration["GOOGLE_CLIENT_SECRET"];
        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.ClaimActions.MapJsonKey("email_verified", "verified_email");
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });
        }
        builder.Services.ConfigureApplicationCookie(o =>
            {
                o.LoginPath = "/account/login";
                o.AccessDeniedPath = "/account/access-denied";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = 401;
                    }
                    else
                    {
                        ctx.Response.Redirect(ctx.RedirectUri);
                    }
                    return Task.CompletedTask;
                };
                o.Events.OnRedirectToAccessDenied = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = 403;
                    }
                    else
                    {
                        ctx.Response.Redirect(ctx.RedirectUri);
                    }
                    return Task.CompletedTask;
                };
            });

        builder.Services.AddIdentityCore<User>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(15));
        builder.Services.AddAuthorizationBuilder().AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
    }

    private static void AddCommerce(WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient("MetaCAPI", client => client.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
        builder.Services.AddHttpClient("Brevo", client => client.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<MetaAdsOptions>(builder.Configuration.GetSection(MetaAdsOptions.SectionName));
        builder.Services.AddSingleton<IMetaCapiQueue, MetaCapiQueue>();
        builder.Services.AddHostedService<MetaCapiBackgroundWorker>();
        builder.Services.AddScoped<IMetaConversionsApiService, MetaConversionsApiService>();
        builder.Services.AddSingleton<IVisitorAnalyticsQueue, VisitorAnalyticsQueue>();
        builder.Services.AddHostedService<VisitorAnalyticsBackgroundWorker>();
        builder.Services.AddHostedService<CartCleanupBackgroundService>();
        builder.Services.AddScoped<IVisitorAnalyticsService, VisitorAnalyticsService>();
        builder.Services.AddScoped<CatalogService>();
        builder.Services.AddScoped<ShoppingService>();
        builder.Services.AddScoped<AdminService>();
        builder.Services.AddScoped<SalesMetricsService>();
        builder.Services.AddScoped<AdminSession>();
        builder.Services.AddScoped<IAdminCatalogBackend, ServerAdminCatalogBackend>();
        builder.Services.AddScoped<IAdminProductWorkspaceBackend, ServerAdminProductWorkspaceBackend>();
        builder.Services.AddScoped<ProductWorkspaceService>();
        builder.Services.AddSingleton<CatalogContentRenderer>();
        builder.Services.AddScoped<AccountMailService>();
        builder.Services.AddScoped<CouponService>();
        builder.Services.AddScoped<IStockNotificationMailTransport, StockNotificationMailTransport>();
        builder.Services.AddScoped<StockNotificationService>();
        builder.Services.AddHostedService<StockNotificationWorker>();
        builder.Services.AddScoped<DeliverySettingsService>();
        builder.Services.AddSingleton<ISeoService, SeoService>();
        builder.Services.AddScoped<SitemapService>();
        builder.Services.AddScoped<EmailVerificationService>();
        builder.Services.AddScoped<AccountRegistrationService>();
        builder.Services.AddSingleton<CourierLogisticsService>();
        builder.Services.AddSingleton<IImageOptimizerService, ImageOptimizerService>();
        builder.Services.AddSingleton<IStorageService, CloudinaryStorageService>();
    }
}
