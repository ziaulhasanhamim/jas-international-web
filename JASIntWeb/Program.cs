using JASIntWeb.Client.Services;
using JASIntWeb.Components;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Endpoints;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
var builder = WebApplication.CreateBuilder(args);

var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portEnv))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");
}
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ICartBackend, ServerCartBackend>();
builder.AddStoreHosting();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddMemoryCache();
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };
});
#pragma warning restore EXTEXP0018

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
         .Tag("catalog"));

    options.AddPolicy("SitemapPolicy", b =>
        b.Expire(TimeSpan.FromHours(12)).Tag("seo"));
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<ShoppingService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SalesMetricsService>();
builder.Services.AddScoped<AdminSession>();
builder.Services.AddScoped<AccountMailService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<DeliverySettingsService>();
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddSingleton<CourierLogisticsService>();
builder.Services.AddSingleton<IStorageService, CloudinaryStorageService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    })
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 1024 * 1024;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
        options.EnableDetailedErrors = true;
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
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null;
});

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(ResolvePostgresConnectionString(builder.Configuration)));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
var app = builder.Build();
app.UseForwardedHeaders();
app.UseWebSockets();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
if (app.Environment.IsDevelopment() 
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrations", false)
    || builder.Configuration.GetValue<bool>("APPLY_MIGRATIONS", false))
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

// Enforce clean canonical URLs: Redirect legacy /products/{slug} to /category/{slug}
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (string.Equals(path, "/category", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "/category/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = $"/products{context.Request.QueryString}";
        return;
    }
    if (path.StartsWith("/products/", StringComparison.OrdinalIgnoreCase))
    {
        var slug = path["/products/".Length..];
        if (!string.IsNullOrWhiteSpace(slug))
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = $"/category/{slug}{context.Request.QueryString}";
            return;
        }
    }
    await next();
});
app.UseRateLimiter();
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();
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

app.MapPost("/api/admin/uploads/product-images", async (HttpRequest request, IStorageService storage) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Form content required" });

    var form = await request.ReadFormAsync();
    var files = form.Files;
    if (files.Count == 0)
        return Results.BadRequest(new { error = "No files uploaded" });

    string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif"];
    var urls = new List<string>();

    foreach (var file in files)
    {
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024) continue;
        var ext = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

        await using var stream = file.OpenReadStream();
        var url = await storage.UploadFileAsync(stream, file.FileName, file.ContentType);
        urls.Add(url);
    }

    return Results.Ok(new { urls });
}).DisableAntiforgery();

app.MapPost("/api/webhooks/mfs-sms", async (HttpRequest request, AppDbContext db, ShoppingService shop, DeliverySettingsService settingsSvc, ILogger<Program> logger) =>
{
    string message = "";
    string senderNumber = "";
    string secret = "";
    string directTrxId = "";
    string directRef = "";
    decimal directAmount = 0;

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

    // Verify secret if configured
    var settings = await settingsSvc.GetSettingsAsync();
    if (!string.IsNullOrWhiteSpace(settings.SmsWebhookSecret) && !string.IsNullOrWhiteSpace(secret))
    {
        if (!string.Equals(settings.SmsWebhookSecret.Trim(), secret.Trim(), StringComparison.Ordinal))
        {
            return Results.Json(new { success = false, error = "Invalid secret key" }, statusCode: 401);
        }
    }

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

    Order? order = null;
    if (!string.IsNullOrEmpty(refCode))
    {
        order = await db.Orders.Include(o => o.DeliveryAddress).Include(o => o.Items).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.PaymentReferenceCode == refCode || o.PublicToken.ToString().EndsWith(refCode));
    }
    if (order == null && !string.IsNullOrEmpty(trxId))
    {
        order = await db.Orders.Include(o => o.DeliveryAddress).Include(o => o.Items).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.TransactionId == trxId);
    }
    if (order == null && !string.IsNullOrEmpty(fromPhone))
    {
        order = await db.Orders.Include(o => o.DeliveryAddress).Include(o => o.Items).Include(o => o.Payments)
            .Where(o => o.SenderPhoneNumber == fromPhone && o.PaidAmount == 0)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }

    if (order != null)
    {
        var verified = await shop.VerifyPaymentAsync(order.Id, amount > 0 ? amount : null, !string.IsNullOrEmpty(trxId) ? trxId : null);
        logger.LogInformation("SMS Webhook: Payment verified for Order #{OrderId} (TrxID: {TrxId}, Amount: {Amount})", verified.Id, verified.TransactionId, verified.PaidAmount);

        return Results.Ok(new
        {
            success = true,
            matched = true,
            orderId = verified.Id,
            reference = verified.PaymentReferenceCode,
            trxId = verified.TransactionId,
            amountPaid = verified.PaidAmount,
            orderStatus = "Pending",
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
        isPaid = order.PaidAmount > 0 || order.Status == OrderStatus.Processing || order.Status == OrderStatus.InTransit || order.Status == OrderStatus.Delivered,
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
