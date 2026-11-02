using JASIntWeb.Client.Services;
using JASIntWeb.Components;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Endpoints;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Identity;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ICartBackend, ServerCartBackend>();
builder.AddStoreHosting();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddMemoryCache();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<ShoppingService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SalesMetricsService>();
builder.Services.AddScoped<AdminSession>();
builder.Services.AddScoped<AccountMailService>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = IdentityConstants.ApplicationScheme;
        o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
    {
        o.LoginPath = "/account/login";
        o.AccessDeniedPath = "/account/access-denied";
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

builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAuthorizationBuilder().AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration["DB_URL"] ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Set DB_URL or ConnectionStrings:DefaultConnection to your PostgreSQL connection string.")));
var app = builder.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:Enabled", true))
{
    await app.Services.SeedTestDataAsync<AppDbContext>(Path.Combine(app.Environment.ContentRootPath, "data", "JasTestSeedData.json"));
}
await app.Services.SeedAdminAsync(builder.Configuration);
await app.Services.SeedBrandsAsync();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        if (context.Request.Path.StartsWithSegments("/account") || context.Request.Path.StartsWithSegments("/admin")
            ||
            context.Request.Path.StartsWithSegments("/checkout")
            || context.Request.Path.StartsWithSegments("/order"))
        {
            context.Response.Headers.CacheControl = "no-store";
        }
        await next();
    });
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CartRequestMiddleware>();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapStoreEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(JASIntWeb.Client._Imports).Assembly);
app.Run();
