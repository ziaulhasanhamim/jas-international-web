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
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<DeliverySettingsService>();
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}
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
await app.Services.SeedCommerceEnhancementsAsync();
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
app.UseStaticFiles();
app.MapStaticAssets();

app.MapPost("/api/admin/uploads/product-images", async (HttpRequest request, IWebHostEnvironment env) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Form content required" });

    var form = await request.ReadFormAsync();
    var files = form.Files;
    if (files.Count == 0)
        return Results.BadRequest(new { error = "No files uploaded" });

    var uploadFolder = Path.Combine(env.WebRootPath, "uploads", "products");
    Directory.CreateDirectory(uploadFolder);

    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif" };
    var urls = new List<string>();

    foreach (var file in files)
    {
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024) continue;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext)) continue;

        var filename = $"prod-{DateTime.UtcNow.Ticks}-{Guid.NewGuid():N}[..8]{ext}";
        var filePath = Path.Combine(uploadFolder, filename);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        urls.Add($"/uploads/products/{filename}");
    }

    return Results.Ok(new { urls });
}).DisableAntiforgery();

app.MapPost("/api/webhooks/mfs-sms", async (HttpRequest request, AppDbContext db, ILogger<Program> logger) =>
{
    string message = "";
    string senderNumber = "";
    string secret = "";

    if (request.HasJsonContentType())
    {
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body);
        if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString() ?? "";
        else if (doc.RootElement.TryGetProperty("text", out var t)) message = t.GetString() ?? "";
        else if (doc.RootElement.TryGetProperty("sms", out var s)) message = s.GetString() ?? "";

        if (doc.RootElement.TryGetProperty("sender", out var snd)) senderNumber = snd.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("secret", out var sec)) secret = sec.GetString() ?? "";
    }
    else if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        message = form["message"].ToString();
        if (string.IsNullOrEmpty(message)) message = form["text"].ToString();
        senderNumber = form["sender"].ToString();
        secret = form["secret"].ToString();
    }

    if (string.IsNullOrWhiteSpace(message))
        return Results.BadRequest(new { error = "No message provided" });

    var amountMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:Tk|Tk\.|BDT|amount)\s*([\d\.]+)|\b([\d\.]+)\s*(?:Tk|BDT)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    var refMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:Ref|Reference|Ref:)\s*([A-Za-z0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    var trxMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:TrxID|TxnId|TxnID|Transaction ID|ID)\s*:?\s*([A-Za-z0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    var phoneMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:from|sender)\s*(01\d{9})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    decimal amount = 0;
    if (amountMatch.Success)
    {
        var val = amountMatch.Groups[1].Success ? amountMatch.Groups[1].Value : amountMatch.Groups[2].Value;
        decimal.TryParse(val, out amount);
    }
    string refCode = refMatch.Success ? refMatch.Groups[1].Value : "";
    string trxId = trxMatch.Success ? trxMatch.Groups[1].Value : "";
    string fromPhone = phoneMatch.Success ? phoneMatch.Groups[1].Value : senderNumber;

    Order? order = null;
    if (!string.IsNullOrEmpty(refCode))
    {
        order = await db.Orders.FirstOrDefaultAsync(o => o.PaymentReferenceCode == refCode || o.PublicToken.ToString().EndsWith(refCode));
    }
    if (order == null && !string.IsNullOrEmpty(trxId))
    {
        order = await db.Orders.FirstOrDefaultAsync(o => o.TransactionId == trxId);
    }
    if (order == null && !string.IsNullOrEmpty(fromPhone) && amount > 0)
    {
        order = await db.Orders.Where(o => o.SenderPhoneNumber == fromPhone && o.Status == OrderStatus.Pending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }

    if (order != null)
    {
        order.Status = OrderStatus.Processing;
        if (!string.IsNullOrEmpty(trxId)) order.TransactionId = trxId;
        if (amount > 0) order.PaidAmount = amount;
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true, matched = true, orderId = order.Id, trxId, amount });
    }

    return Results.Ok(new { success = true, matched = false, parsed = new { amount, refCode, trxId, fromPhone } });
}).DisableAntiforgery();

app.MapGet("/api/orders/{id:int}/payment-status", async (int id, AppDbContext db) =>
{
    var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    if (order == null) return Results.NotFound();
    return Results.Ok(new
    {
        id = order.Id,
        status = order.Status.ToString(),
        isPaid = order.PaidAmount > 0 || order.Status == OrderStatus.Processing || order.Status == OrderStatus.InTransit || order.Status == OrderStatus.Delivered,
        paidAmount = order.PaidAmount,
        trxId = order.TransactionId
    });
});

app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nAllow: /\nDisallow: /admin/\nDisallow: /account/\nSitemap: https://jasinternational.com/sitemap.xml\n", "text/plain"));

app.MapGet("/sitemap.xml", async (AppDbContext db, HttpRequest request) =>
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var products = await db.Products.AsNoTracking().Where(p => !p.IsDeleted).Select(p => p.Slug).ToListAsync();
    var categories = await db.Categories.AsNoTracking().Where(c => !c.IsDeleted).Select(c => c.Slug).ToListAsync();
    var brands = await db.Brands.AsNoTracking().Where(b => !b.IsDeleted).Select(b => b.Slug).ToListAsync();

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    sb.AppendLine($"  <url><loc>{baseUrl}/</loc><changefreq>daily</changefreq><priority>1.0</priority></url>");
    sb.AppendLine($"  <url><loc>{baseUrl}/products</loc><changefreq>daily</changefreq><priority>0.9</priority></url>");
    sb.AppendLine($"  <url><loc>{baseUrl}/about</loc><priority>0.5</priority></url>");
    sb.AppendLine($"  <url><loc>{baseUrl}/contact</loc><priority>0.5</priority></url>");
    sb.AppendLine($"  <url><loc>{baseUrl}/faq</loc><priority>0.5</priority></url>");

    foreach (var cat in categories)
        sb.AppendLine($"  <url><loc>{baseUrl}/category/{cat}</loc><changefreq>daily</changefreq><priority>0.8</priority></url>");

    foreach (var brand in brands)
        sb.AppendLine($"  <url><loc>{baseUrl}/products?brand={brand}</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");

    foreach (var prod in products)
        sb.AppendLine($"  <url><loc>{baseUrl}/products/{prod}</loc><changefreq>daily</changefreq><priority>0.9</priority></url>");

    sb.AppendLine("</urlset>");
    return Results.Text(sb.ToString(), "application/xml");
});
app.MapStoreEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(JASIntWeb.Client._Imports).Assembly);
app.Run();
