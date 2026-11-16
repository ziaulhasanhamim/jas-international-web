using JASIntWeb.Client.Services;
using JASIntWeb.Components;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Endpoints;
using JASIntWeb.Middleware;
using JASIntWeb.Services;
using JASIntWeb.Services.Analytics;
using JASIntWeb.Services.Meta;
using JASIntWeb.Services.Seo;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Hybrid;
var builder = WebApplication.CreateBuilder(args);

var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portEnv))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");
}
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ICartBackend, ServerCartBackend>();
builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.AddStoreHosting();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
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
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("MetaCAPI");
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
builder.Services.AddScoped<AccountMailService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<IStockNotificationMailTransport, StockNotificationMailTransport>();
builder.Services.AddScoped<StockNotificationService>();
builder.Services.AddHostedService<StockNotificationWorker>();
builder.Services.AddScoped<DeliverySettingsService>();
builder.Services.AddSingleton<ISeoService, SeoService>();
builder.Services.AddScoped<SitemapService>();
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddSingleton<CourierLogisticsService>();
builder.Services.AddSingleton<IImageOptimizerService, ImageOptimizerService>();
builder.Services.AddSingleton<IStorageService, CloudinaryStorageService>();
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
    });
}
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

builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(15));
builder.Services.AddAuthorizationBuilder().AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

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

// Built-in .NET Health Check with EF Core Database Connectivity Check
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: ["ready", "db"]);

// Pooled DbContext Factory: pools DbContext instances to eliminate instantiation overhead and reduce GC allocations
builder.Services.AddPooledDbContextFactory<AppDbContext>(o => o.UseNpgsql(ResolvePostgresConnectionString(builder.Configuration)));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
var app = builder.Build();
app.UseForwardedHeaders();
app.UseWebSockets();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
var applyMigrations = builder.Configuration.GetValue<bool>("Database:ApplyMigrations", true)
    && !builder.Configuration.GetValue<bool>("Database:SkipMigrations", false);
if (applyMigrations)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while applying database migrations on startup.");
    }
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:Enabled", true))
{
    await app.Services.SeedTestDataAsync<AppDbContext>(Path.Combine(app.Environment.ContentRootPath, "data", "JasTestSeedData.json"));
    await app.Services.SeedBrandsAsync();
    await app.Services.SeedCommerceEnhancementsAsync();
}
await app.Services.SeedAdminAsync(builder.Configuration);
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
    branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.Use(async (context, next) =>
{
    var response = context.Response;
    var headers = response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    var path = context.Request.Path;
    if (path.StartsWithSegments("/account")
        || path.StartsWithSegments("/admin")
        || path.StartsWithSegments("/checkout")
        || path.StartsWithSegments("/order"))
    {
        response.OnStarting(static state =>
        {
            var respHeaders = ((HttpResponse)state).Headers;
            if (string.IsNullOrEmpty(respHeaders.CacheControl))
            {
                respHeaders.CacheControl = "no-store";
                respHeaders.Pragma = "no-cache";
            }
            return Task.CompletedTask;
        }, response);
    }

    await next();
});
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

app.MapGet("/api/admin/uploads/security", (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery csrf) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken });
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/admin/uploads/product-images", async (HttpRequest request, IStorageService storage, Microsoft.AspNetCore.Antiforgery.IAntiforgery csrf, ILogger<Program> uploadLogger) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Form content required" });

    try { await csrf.ValidateRequestAsync(request.HttpContext); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(new { error = "Refresh the page and try again." }); }
    var form = await request.ReadFormAsync();
    var files = form.Files;
    if (files.Count == 0)
        return Results.BadRequest(new { error = "No files uploaded" });

    string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif", ".svg"];
    if (files.Count > 10) return Results.BadRequest(new { error = "Upload up to 10 images at a time." });
    var urls = new List<string>();

    try
    {
        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > 10 * 1024 * 1024) return Results.BadRequest(new { error = "Each image must be between 1 byte and 10MB." });
            var ext = Path.GetExtension(file.FileName);
            if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "Unsupported image format." });

            await using var stream = file.OpenReadStream();
            var url = await ImageUploadValidator.UploadAsync(storage, stream, file.FileName, file.ContentType, request.HttpContext.RequestAborted);
            urls.Add(url);
        }

        return Results.Ok(new { urls });
    }
    catch (Exception error)
    {
        uploadLogger.LogWarning(error, "Admin image upload failed.");
        return Results.BadRequest(new { error = "The image could not be uploaded. Check its format and try again." });
    }
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/admin/uploads/brand-logo", async (HttpRequest request, IStorageService storage, Microsoft.AspNetCore.Antiforgery.IAntiforgery csrf, ILogger<Program> uploadLogger) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Form content required" });

    try { await csrf.ValidateRequestAsync(request.HttpContext); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(new { error = "Refresh the page and try again." }); }
    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    if (file.Length > 5 * 1024 * 1024)
        return Results.BadRequest(new { error = "Logo file exceeds 5MB limit" });

    string[] allowedExtensions = [".svg", ".png", ".webp", ".jpg", ".jpeg", ".avif", ".gif"];
    var ext = Path.GetExtension(file.FileName);
    if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Unsupported image format. Please upload SVG, PNG, WEBP, or JPG." });

    try
    {
        await using var stream = file.OpenReadStream();
        var url = await ImageUploadValidator.UploadAsync(storage, stream, file.FileName, file.ContentType, request.HttpContext.RequestAborted);
        return Results.Ok(new { url });
    }
    catch (Exception error)
    {
        uploadLogger.LogWarning(error, "Admin image upload failed.");
        return Results.BadRequest(new { error = "The image could not be uploaded. Check its format and try again." });
    }
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/admin/uploads/image", async (HttpRequest request, IStorageService storage, Microsoft.AspNetCore.Antiforgery.IAntiforgery csrf, ILogger<Program> uploadLogger) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Form content required" });

    try { await csrf.ValidateRequestAsync(request.HttpContext); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(new { error = "Refresh the page and try again." }); }
    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    if (file.Length > 10 * 1024 * 1024)
        return Results.BadRequest(new { error = "Image file exceeds 10MB limit" });

    string[] allowedExtensions = [".svg", ".png", ".webp", ".jpg", ".jpeg", ".avif", ".gif"];
    var ext = Path.GetExtension(file.FileName);
    if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Unsupported image format. Please upload SVG, PNG, WEBP, AVIF, or JPG." });

    try
    {
        await using var stream = file.OpenReadStream();
        var url = await ImageUploadValidator.UploadAsync(storage, stream, file.FileName, file.ContentType, request.HttpContext.RequestAborted);
        return Results.Ok(new { url });
    }
    catch (Exception error)
    {
        uploadLogger.LogWarning(error, "Admin image upload failed.");
        return Results.BadRequest(new { error = "The image could not be uploaded. Check its format and try again." });
    }
}).RequireAuthorization("AdminOnly");

app.MapPost("/api/webhooks/mfs-sms", async (HttpRequest request, AppDbContext db, ShoppingService shop, DeliverySettingsService settingsSvc, ILogger<Program> logger) =>
{
    string message = "";
    string senderNumber = "";
    string secret = "";
    string directTrxId = "";
    string directRef = "";
    decimal directAmount = 0;

    try
    {
    if (request.HasJsonContentType())
    {
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body);
        var root = doc.RootElement;
        if (root.TryGetProperty("message", out var m)) message = m.GetString() ?? "";
        else if (root.TryGetProperty("text", out var t)) message = t.GetString() ?? "";
        else if (root.TryGetProperty("sms", out var s)) message = s.GetString() ?? "";

        if (root.TryGetProperty("sender", out var snd)) senderNumber = snd.GetString() ?? "";
        else if (root.TryGetProperty("phone", out var ph)) senderNumber = ph.GetString() ?? "";

        if (root.TryGetProperty("secret", out var sec)) secret = sec.GetString() ?? "";

        if (root.TryGetProperty("trxId", out var tx1)) directTrxId = tx1.GetString() ?? "";
        else if (root.TryGetProperty("trx_id", out var tx2)) directTrxId = tx2.GetString() ?? "";
        else if (root.TryGetProperty("transactionId", out var tx3)) directTrxId = tx3.GetString() ?? "";

        if (root.TryGetProperty("reference", out var rf1)) directRef = rf1.GetString() ?? "";
        else if (root.TryGetProperty("ref", out var rf2)) directRef = rf2.GetString() ?? "";

        if (root.TryGetProperty("amount", out var am))
        {
            if (am.ValueKind == System.Text.Json.JsonValueKind.Number) directAmount = am.GetDecimal();
            else if (am.ValueKind == System.Text.Json.JsonValueKind.String) decimal.TryParse(am.GetString(), out directAmount);
        }
    }
    else if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        message = form["message"].ToString();
        if (string.IsNullOrEmpty(message)) message = form["text"].ToString();
        senderNumber = form["sender"].ToString();
        if (string.IsNullOrEmpty(senderNumber)) senderNumber = form["phone"].ToString();
        secret = form["secret"].ToString();
        directTrxId = form["trxId"].ToString();
        if (string.IsNullOrEmpty(directTrxId)) directTrxId = form["transactionId"].ToString();
        directRef = form["reference"].ToString();
        if (string.IsNullOrEmpty(directRef)) directRef = form["ref"].ToString();
        decimal.TryParse(form["amount"].ToString(), out directAmount);
    }

    }
    catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException or FormatException or OverflowException)
    {
        return Results.BadRequest(new { error = "Invalid payment notification." });
    }

    var settings = await settingsSvc.GetSettingsAsync();
    if (string.IsNullOrWhiteSpace(settings.SmsWebhookSecret) || settings.SmsWebhookSecret is "jas_sms_secret_2026" or "jas_secret_sms_2026")
        return Results.Json(new { error = "Payment webhook is disabled." }, statusCode: 503);
    var expectedSecret = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(settings.SmsWebhookSecret));
    var actualSecret = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
    if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedSecret, actualSecret))
        return Results.Unauthorized();

    decimal amount = directAmount;
    string refCode = directRef;
    string trxId = directTrxId;
    string fromPhone = senderNumber;

    if (!string.IsNullOrWhiteSpace(message))
    {
        var amountMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:Tk|Tk\.|BDT|amount)\s*([\d\.]+)|\b([\d\.]+)\s*(?:Tk|BDT)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var refMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:Ref|Reference|Ref:)\s*([A-Za-z0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var trxMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:TrxID|TxnId|TxnID|Transaction ID|ID)\s*:?\s*([A-Za-z0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var phoneMatch = System.Text.RegularExpressions.Regex.Match(message, @"(?:from|sender)\s*(01\d{9})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (amountMatch.Success && amount == 0)
        {
            var val = amountMatch.Groups[1].Success ? amountMatch.Groups[1].Value : amountMatch.Groups[2].Value;
            decimal.TryParse(val, out amount);
        }
        if (refMatch.Success && string.IsNullOrEmpty(refCode)) refCode = refMatch.Groups[1].Value;
        if (trxMatch.Success && string.IsNullOrEmpty(trxId)) trxId = trxMatch.Groups[1].Value;
        if (phoneMatch.Success && string.IsNullOrEmpty(fromPhone)) fromPhone = phoneMatch.Groups[1].Value;
    }

    if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(trxId) && string.IsNullOrWhiteSpace(refCode))
    {
        return Results.BadRequest(new { error = "No message, TrxID, or reference provided" });
    }

    trxId = trxId.Trim().ToUpperInvariant();
    refCode = refCode.Trim();
    if (amount <= 0 || amount > 10000000m || string.IsNullOrWhiteSpace(trxId) || trxId.Length < 6)
        return Results.BadRequest(new { error = "A positive amount and valid transaction ID are required." });
    var matches = await db.Orders.AsNoTracking()
        .Where(o => (!string.IsNullOrEmpty(refCode) && o.PaymentReferenceCode == refCode)
            || o.TransactionId == trxId)
        .Select(o => o.Id).Distinct().Take(2).ToListAsync();
    if (matches.Count != 1)
        return Results.Conflict(new { error = "No unambiguous order match. Review this payment manually." });
    var order = await db.Orders.FindAsync(matches[0]);

    if (order != null)
    {
        Order verified;
        try { verified = await shop.VerifyPaymentAsync(order.Id, amount, trxId); }
        catch (ShopException ex) { return Results.Conflict(new { error = ex.Message }); }
        logger.LogInformation("SMS Webhook: Payment verified for Order #{OrderId} (TrxID: {TrxId}, Amount: {Amount})", verified.Id, verified.TransactionId, verified.PaidAmount);

        return Results.Ok(new
        {
            success = true,
            matched = true,
            orderId = verified.Id,
            reference = verified.PaymentReferenceCode,
            trxId = verified.TransactionId,
            amountPaid = verified.PaidAmount,
            orderStatus = verified.Status.ToString(),
            paymentStatus = "Verified",
            message = "Payment verified successfully. Order status remains Pending until confirmed by admin in Admin Panel."
        });
    }

    return Results.Ok(new
    {
        success = true,
        matched = false,
        message = "No matching pending order found. Check reference code, TrxID, or sender number.",
        parsed = new { amount, refCode, trxId, fromPhone }
    });
}).DisableAntiforgery();

app.MapGet("/api/orders/{id:int}/payment-status", async (int id, AppDbContext db) =>
{
    var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    if (order == null) return Results.NotFound();
    return Results.Ok(new
    {
        id = order.Id,
        status = order.Status.ToString(),
        isPaid = order.PaidAmount > 0 && await db.Payments.AnyAsync(p => p.OrderId == id && p.Status == PaymentStatus.Completed),
        paidAmount = order.PaidAmount,
        trxId = order.TransactionId
    });
});

app.MapPost("/account/logout", async (
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    HttpContext http,
    [FromForm] string? returnUrl) =>
{
    var user = await userManager.GetUserAsync(http.User);
    if (user is not null)
    {
        await userManager.UpdateSecurityStampAsync(user);
    }
    await signInManager.SignOutAsync();
    var target = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//") ? "/" : returnUrl;
    return Results.LocalRedirect(target);
});

app.MapGet("/account/external-login", (string provider, string? returnUrl, SignInManager<User> signInManager) =>
{
    var redirectUrl = $"/account/external-login-callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Results.Challenge(properties, [provider]);
});

app.MapGet("/account/external-login-callback", async (
    string? returnUrl,
    string? remoteError,
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    ILogger<Program> logger) =>
{
    returnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    if (!string.IsNullOrEmpty(remoteError))
    {
        logger.LogWarning("External login callback error: {RemoteError}", remoteError);
        return Results.Redirect($"/account/login?error={Uri.EscapeDataString($"Error from Google: {remoteError}")}");
    }

    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info == null)
    {
        logger.LogWarning("GetExternalLoginInfoAsync returned null.");
        return Results.Redirect($"/account/login?error={Uri.EscapeDataString("Failed to receive authentication details from Google. Please try again.")}");
    }

    // Try signing in if provider login already linked
    var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
    if (signInResult.Succeeded)
    {
        logger.LogInformation("User logged in with Google via linked external login.");
        return Results.Redirect(returnUrl);
    }

    if (signInResult.IsLockedOut)
    {
        return Results.Redirect("/account/access-denied");
    }

    // Match by email or create new user
    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.Redirect($"/account/login?error={Uri.EscapeDataString("Google did not provide an email address.")}");
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true // Verified by Google
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var msg = string.Join(", ", createResult.Errors.Select(e => e.Description));
            return Results.Redirect($"/account/login?error={Uri.EscapeDataString(msg)}");
        }

        var name = info.Principal.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            await userManager.AddClaimAsync(user, new Claim("name", name));
        }
    }
    else
    {
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
    }

    var addLoginResult = await userManager.AddLoginAsync(user, info);
    if (!addLoginResult.Succeeded)
    {
        logger.LogDebug("AddLogin result: {Errors}", string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
    }

    await signInManager.SignInAsync(user, isPersistent: true, info.LoginProvider);
    return Results.Redirect(returnUrl);
});

app.MapStoreEndpoints();
app.MapCustomerConfidenceEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(JASIntWeb.Client._Imports).Assembly);
app.Run();

static string ResolvePostgresConnectionString(IConfiguration config)
{
    var raw = config["DATABASE_URL"]
        ?? config["DB_URL"]
        ?? config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Set DATABASE_URL, DB_URL, or ConnectionStrings:DefaultConnection to your PostgreSQL connection string.");

    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = user,
                Password = password,
                SslMode = Npgsql.SslMode.Require,
                Pooling = true
            };

            if (!string.IsNullOrEmpty(uri.Query))
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("channel_binding", out var cbVal) && Enum.TryParse<Npgsql.ChannelBinding>(cbVal, true, out var cb))
                {
                    npgsqlBuilder.ChannelBinding = cb;
                }
            }

            return npgsqlBuilder.ConnectionString;
        }
        catch
        {
            return raw;
        }
    }

    return raw;
}
