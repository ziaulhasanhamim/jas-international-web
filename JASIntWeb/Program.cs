using JASIntWeb.Client.Services;
using JASIntWeb.Components;
using JASIntWeb.Client.Contracts;
using JASIntWeb.Endpoints;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ICartBackend, ServerCartBackend>();
builder.AddStoreHosting();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddMemoryCache();
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
builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();
builder.Services.AddCascadingAuthenticationState();
var authBuilder = builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = IdentityConstants.ApplicationScheme;
        o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    });
authBuilder.AddIdentityCookies();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
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

    string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif"];
    var urls = new List<string>();

    foreach (var file in files)
    {
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024) continue;
        var ext = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

        var filename = $"prod-{DateTime.UtcNow.Ticks}-{Guid.NewGuid().ToString("N")[..8]}{ext.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadFolder, filename);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        urls.Add($"/uploads/products/{filename}");
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
