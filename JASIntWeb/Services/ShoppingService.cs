using System.Data;
using System.Security.Claims;
using JASIntWeb.Helpers;
using JASIntWeb.Middleware;
using JASIntWeb.Services.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JASIntWeb.Services;

public sealed class ShoppingService(
    AppDbContext db,
    IConfiguration config,
    DeliverySettingsService deliverySettings,
    CouponService couponService,
    AccountMailService mailService,
    IMetaConversionsApiService? metaApi = null,
    IHttpContextAccessor? httpContextAccessor = null,
    ILogger<ShoppingService>? logger = null,
    CatalogService? catalog = null)
{
    public static int? UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public DeliveryRatesDto Rates => new(config.GetValue<decimal>("Delivery:Khulna", 50), config.GetValue<decimal>("Delivery:OutsideKhulna", 150));

    public async Task LockUserAsync(int userId) => await db.Database.ExecuteSqlAsync(
        $"SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE");

    public async Task<Cart> ActiveCartAsync(Guid? token, int? userId)
    {
        Cart? cart = null;

        if (userId.HasValue)
        {
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.Status == CartStatus.Active);

            if (cart is null && token.HasValue && token.Value != Guid.Empty)
            {
                cart = await db.Carts.Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.Token == token.Value && c.Status == CartStatus.Active);
                if (cart != null)
                {
                    cart.UserId = userId.Value;
                }
            }
        }
        else if (token.HasValue && token.Value != Guid.Empty)
        {
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Token == token.Value && c.Status == CartStatus.Active);
        }

        if (cart is not null)
        {
            cart.LastActivityAt = DateTimeOffset.UtcNow;
            return cart;
        }

        var canUseToken = token.HasValue && token.Value != Guid.Empty && !await db.Carts.AnyAsync(c => c.Token == token.Value);
        cart = new Cart
        {
            Token = canUseToken ? token!.Value : Guid.CreateVersion7(),
            UserId = userId,
            Status = CartStatus.Active,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return cart;
    }

    private Task<Cart> ActiveCartAsync(int userId) => ActiveCartAsync(null, userId);

    public async Task<Result<CartDto>> ResolveAsync(List<CartLineRequest> lines, bool authenticated = false)
    {
        if (lines.Count > 100)
        {
            return new ShopError("A cart can contain at most 100 variants.");
        }
        if (lines.Any(l => l.VariantId <= 0 || l.Quantity <= 0 || l.Quantity > 99))
        {
            return new ShopError("Quantity must be between 1 and 99.");
        }
        var errorFound = false;
        var normalized = lines.GroupBy(l => l.VariantId).Select(g =>
            {
                var total = g.Sum(l => l.Quantity);
                if (total > 99)
                {
                    errorFound = true;
                }
                return new CartLineRequest(g.Key, total);
            })
            .ToList();
        if (errorFound)
        {
            return new ShopError("Quantity must be between 1 and 99.");
        }
        if (normalized.Count == 0)
        {
            return new CartDto(authenticated, []);
        }
        var ids = normalized.Select(l => l.VariantId).ToArray();
        var variants = await db.ProductVariants.AsNoTracking().Where(v => ids.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
                v.SKU,
                v.SellingPrice,
                v.Product!.Name,
                v.Product.Slug,
                v.Product.MainImageUrl,
                Available = v.IsActive && !v.IsDeleted && !v.Product.IsDeleted && !v.Product.Category!.IsDeleted
                &&
                (v.Product.Category.ParentCategoryId == null || !v.Product.Category.ParentCategory!.IsDeleted),
                Stock = db.InventoryItems.Where(i => i.ProductVariantId == v.Id)
                .Select(i => i.QuantityOnHand - i.QuantityReserved)
                .FirstOrDefault(),
                Options = v.Options.OrderBy(o => o.ProductAttributeOption!.ProductAttributeDefinition!.SortOrder)
                .ThenBy(o => o.ProductAttributeDefinitionId)
                .Select(o => o.ProductAttributeOption!.Value)
                .ToList()
            })
            .ToDictionaryAsync(v => v.Id);
        return new CartDto(authenticated, [.. normalized.Select(l =>
                {
                    var v = variants.GetValueOrDefault(l.VariantId);
                    var available = v?.Available == true;
                    var sku = (!string.IsNullOrWhiteSpace(v?.SKU) ? v.SKU : (v != null ? $"V{v.Id}" : $"V{l.VariantId}")).Trim().ToUpperInvariant();
                    return new CartItemDto(l.VariantId, v?.Name ?? "Unavailable product", v?.Slug ?? "",
                        v?.MainImageUrl ?? "", v is null ? "" : string.Join(" / ", v.Options),
                        v?.SellingPrice ?? 0, l.Quantity, available ? v!.Stock : 0, available, sku);
                })]);
    }

    public async Task<int?> RecordLeadCartAsync(int? userId, string phone, string? name, string? email, List<CartItemDto> items, MetaAttributionDto? attr = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        phone = phone.Trim();

        try
        {
            Cart? cart = null;
            if (userId.HasValue)
            {
                cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId.Value && c.Status == CartStatus.Active);
            }
            else
            {
                cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == null && c.CustomerPhone == phone && c.Status == CartStatus.Active);
            }

            if (cart != null)
            {
                cart.CustomerPhone = phone;
                if (!string.IsNullOrWhiteSpace(name)) cart.CustomerName = name.Trim();
                if (!string.IsNullOrWhiteSpace(email)) cart.CustomerEmail = email.Trim();
                cart.LastActivityAt = DateTimeOffset.UtcNow;
                if (attr != null)
                {
                    cart.Fbclid = attr.Fbclid ?? cart.Fbclid;
                    cart.Fbp = attr.Fbp ?? cart.Fbp;
                    cart.Fbc = attr.Fbc ?? cart.Fbc;
                    cart.UtmSource = attr.UtmSource ?? cart.UtmSource;
                    cart.UtmCampaign = attr.UtmCampaign ?? cart.UtmCampaign;
                }
                await db.SaveChangesAsync();
                return cart.Id;
            }

            if (items.Count == 0) return null;

            var newCart = new Cart
            {
                UserId = userId,
                Status = CartStatus.Active,
                CustomerPhone = phone,
                CustomerName = name?.Trim(),
                CustomerEmail = email?.Trim(),
                Fbclid = attr?.Fbclid,
                Fbp = attr?.Fbp,
                Fbc = attr?.Fbc,
                UtmSource = attr?.UtmSource,
                UtmMedium = attr?.UtmMedium,
                UtmCampaign = attr?.UtmCampaign,
                LastActivityAt = DateTimeOffset.UtcNow,
                Items = [.. items.Select(i => new CartItem
                {
                    ProductVariantId = i.VariantId,
                    Quantity = i.Quantity
                })]
            };

            db.Carts.Add(newCart);
            await db.SaveChangesAsync();
            return newCart.Id;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to record lead cart for phone {Phone}", phone);
            return null;
        }
    }

    public async Task<CartDto> GetCartAsync(Guid? token, int? userId)
    {
        Cart? cart = null;

        if (userId.HasValue)
        {
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.Status == CartStatus.Active);

            if (cart is null && token.HasValue && token.Value != Guid.Empty)
            {
                cart = await db.Carts.Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.Token == token.Value && c.Status == CartStatus.Active);
                if (cart != null)
                {
                    cart.UserId = userId.Value;
                    await db.SaveChangesAsync();
                }
            }
        }
        else if (token.HasValue && token.Value != Guid.Empty)
        {
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Token == token.Value && c.Status == CartStatus.Active);
        }

        if (cart is null)
        {
            return new CartDto(userId.HasValue, []);
        }

        var lines = cart.Items.Select(i => new CartLineRequest(i.ProductVariantId, i.Quantity)).ToList();
        var resolved = await ResolveAsync(lines, userId.HasValue);
        return resolved switch
        {
            CartDto dto => dto with { Token = cart.Token },
            Error => new CartDto(userId.HasValue, []) with { Token = cart.Token }
        };
    }

    public Task<CartDto> GetCartAsync(int userId) => GetCartAsync(null, userId);

    public async Task<int> GetCartItemCountAsync(Guid? token, int? userId)
    {
        if (userId.HasValue)
        {
            var userCartCount = await db.CartItems.AsNoTracking()
                .Where(i => i.Cart.UserId == userId.Value && i.Cart.Status == CartStatus.Active)
                .SumAsync(i => (int?)i.Quantity);
            if (userCartCount.HasValue && userCartCount.Value > 0) return userCartCount.Value;
        }

        if (token.HasValue && token.Value != Guid.Empty)
        {
            var tokenCartCount = await db.CartItems.AsNoTracking()
                .Where(i => i.Cart.Token == token.Value && i.Cart.Status == CartStatus.Active)
                .SumAsync(i => (int?)i.Quantity);
            return tokenCartCount ?? 0;
        }

        return 0;
    }

    public async Task<Result<CartDto>> ChangeAsync(Guid? token, int? userId, CartLineRequest line, bool add)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (userId.HasValue)
        {
            await LockUserAsync(userId.Value);
        }
        var cart = await ActiveCartAsync(token, userId);
        var item = cart.Items.FirstOrDefault(i => i.ProductVariantId == line.VariantId);
        var requestedQuantity = add ? (item?.Quantity ?? 0) + line.Quantity : line.Quantity;
        if (requestedQuantity < 0 || requestedQuantity > 99 || (add && line.Quantity <= 0))
        {
            return new ShopError("Quantity must be between 1 and 99.");
        }
        if (requestedQuantity == 0)
        {
            if (item is not null)
            {
                db.CartItems.Remove(item);
            }
        }
        else
        {
            var resolvedRes = await ResolveAsync([new(line.VariantId, requestedQuantity)]);
            CartDto resolved = null!;
            switch (resolvedRes)
            {
                case CartDto dto:
                    resolved = dto;
                    break;
                case Error err:
                    return err;
            }
            var lineStock = resolved.Items[0].Stock;
            if (!resolved.Items[0].IsAvailable || lineStock <= 0)
            {
                return new ShopError("The requested item is currently sold out.");
            }

            int finalQuantity;
            if (add)
            {
                if (item is not null && item.Quantity >= lineStock)
                {
                    return new ShopError($"Maximum available stock ({lineStock}) is already in your cart.");
                }
                finalQuantity = Math.Min(requestedQuantity, lineStock);
            }
            else
            {
                if (requestedQuantity > lineStock)
                {
                    return new ShopError("The requested quantity is unavailable.");
                }
                finalQuantity = requestedQuantity;
            }

            if (item is null)
            {
                if (cart.Items.Count >= 100)
                {
                    return new ShopError("A cart can contain at most 100 variants.");
                }
                cart.Items.Add(new CartItem
                {
                    ProductVariantId = line.VariantId,
                    Quantity = finalQuantity
                });
            }
            else
            {
                item.Quantity = finalQuantity;
            }
        }
        cart.LastActivityAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();
        return await GetCartAsync(cart.Token, userId);
    }

    public Task<Result<CartDto>> ChangeAsync(int userId, CartLineRequest line, bool add) => ChangeAsync(null, userId, line, add);

    public async Task<Result<CartDto>> MergeAsync(int userId, CartMergeRequest request)
    {
        if (request.Token == Guid.Empty)
        {
            return new ShopError("The cart merge token is missing.");
        }
        var resolvedRes = await ResolveAsync(request.Items);
        CartDto resolved = null!;
        switch (resolvedRes)
        {
            case CartDto dto:
                resolved = dto;
                break;
            case Error err:
                return err;
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockUserAsync(userId);
        if (!await db.CartMergeReceipts.AnyAsync(r => r.UserId == userId && r.Token == request.Token))
        {
            var cart = await ActiveCartAsync(userId);
            foreach (var line in resolved.Items)
            {
                var item = cart.Items.FirstOrDefault(i => i.ProductVariantId == line.VariantId);
                var quantity = Math.Min(99, (item?.Quantity ?? 0) + line.Quantity);
                // Retain unavailable items so the user can review/remove them instead of silently losing them.
                if (item is null)
                {
                    if (!await db.ProductVariants.AnyAsync(v => v.Id == line.VariantId))
                    {
                        continue;
                    }
                    if (cart.Items.Count >= 100)
                    {
                        return new ShopError("A cart can contain at most 100 variants.");
                    }
                    cart.Items.Add(new CartItem
                    {
                        ProductVariantId = line.VariantId,
                        Quantity = quantity
                    });
                }
                else
                {
                    item.Quantity = quantity;
                }
            }
            db.CartMergeReceipts.Add(new()
            {
                UserId = userId,
                Token = request.Token
            });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();
        return await GetCartAsync(userId);
    }

    public async Task ClearAsync(Guid? token, int? userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (userId.HasValue)
        {
            await LockUserAsync(userId.Value);
        }
        var cart = await ActiveCartAsync(token, userId);
        db.CartItems.RemoveRange(cart.Items);
        cart.LastActivityAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();
    }

    public Task ClearAsync(int userId) => ClearAsync(null, userId);

    public async Task<Result<CheckoutResult>> CheckoutAsync(int? userId, CheckoutRequest request)
    {
        var validation = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, new System.ComponentModel.DataAnnotations.ValidationContext(request), validation, true))
        {
            return new ValidationError(string.Join(" ", validation.Select(e => e.ErrorMessage)));
        }
        if (request.RequestId == Guid.Empty)
        {
            return new ShopError("The checkout token is missing.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        // One request token cannot create two orders, including guest retries and concurrent requests.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({request.RequestId.ToString()}, 0))");
        var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.CheckoutToken == request.RequestId);
        if (existing is not null)
        {
            if (existing.UserId != userId)
            {
                return new ConflictError("This checkout token has already been used.");
            }
            await tx.CommitAsync();
            return new CheckoutResult(existing.Id, existing.PublicToken, existing.Total);
        }
        Cart? cart = null;
        if (userId.HasValue)
        {
            await LockUserAsync(userId.Value);
            cart = await ActiveCartAsync(null, userId.Value);
            if (cart.Items.Count > 0)
            {
                request.Items = [.. cart.Items.Select(i => new CartLineRequest(i.ProductVariantId, i.Quantity))];
            }
        }
        else
        {
            Guid? cartToken = request.CartToken.HasValue && request.CartToken.Value != Guid.Empty
                ? request.CartToken.Value
                : (httpContextAccessor?.HttpContext?.Request.Cookies.TryGetValue("jas_cart_id", out var tStr) == true && Guid.TryParse(tStr, out var g) ? g : (Guid?)null);
            if (cartToken.HasValue)
            {
                cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == cartToken.Value && c.Status == CartStatus.Active);
                if (cart != null && cart.Items.Count > 0)
                {
                    request.Items = [.. cart.Items.Select(i => new CartLineRequest(i.ProductVariantId, i.Quantity))];
                }
            }
        }
        if (request.Items.Count == 0)
        {
            return new ShopError("Your cart is empty.");
        }

        // Strict payment validation
        if (request.PaymentMethod is "BkashAdvance" or "FullPayment")
        {
            if (string.IsNullOrWhiteSpace(request.SenderPhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(request.SenderPhoneNumber.Trim(), @"^(?:\+?88)?01[3-9]\d{8}$"))
            {
                return new ValidationError("A valid 11-digit bKash sender phone number is required to confirm payment.");
            }
            if (string.IsNullOrWhiteSpace(request.TransactionId) || request.TransactionId.Trim().Length < 6)
            {
                return new ValidationError("A valid bKash Transaction ID (TrxID) is required to place your order.");
            }
        }
        else if (request.PaymentMethod == "COD")
        {
            var dSettings = await deliverySettings.GetSettingsAsync();
            if (dSettings.RequireAdvanceForCOD)
            {
                return new ShopError("Advance delivery fee is required for cash on delivery orders.");
            }
        }
        else
        {
            return new ShopError("A valid payment method and payment details are required to place an order.");
        }
        var summaryRes = await ResolveAsync(request.Items, userId.HasValue);
        CartDto summary = null!;
        switch (summaryRes)
        {
            case CartDto dto:
                summary = dto;
                break;
            case Error err:
                return err;
        }
        if (summary.Items.Any(i => !i.IsAvailable))
        {
            return new ShopError("One or more cart items are unavailable. Update your cart before checking out.");
        }
        var unitCosts = new Dictionary<int, decimal>();
        foreach (var item in summary.Items.OrderBy(i => i.VariantId))
        {
            // Lock inventory in a stable order. The available quantity is checked again after acquiring the lock.
            await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {item.VariantId} FOR UPDATE");
            var inventory = await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == item.VariantId);
            var variant = await db.ProductVariants.Include(v => v.Product).ThenInclude(p => p!.Category)
                .ThenInclude(c => c!.ParentCategory)
                .SingleAsync(v => v.Id == item.VariantId);
            if (!item.IsAvailable
                || inventory is null
                || inventory.AvailableQuantity < item.Quantity
                || !variant.IsActive
                || variant.IsDeleted
                || variant.Product!.IsDeleted
                || variant.Product.Category!.IsDeleted
                || variant.Product.Category.ParentCategory?.IsDeleted == true)
            {
                return new ShopError($"{item.Name}: the selected quantity is no longer available.");
            }
            // Do not trust a client price or the earlier quote.
            inventory.QuantityReserved += item.Quantity;
            unitCosts[item.VariantId] = variant.CostPrice;
            summary.Items[summary.Items.IndexOf(item)] = item with
            {
                Price = variant.SellingPrice
            };
        }
        var delivery = await deliverySettings.CalculateDeliveryChargeAsync(request.District, summary.Subtotal);

        decimal discount = 0;
        string? appliedCoupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var couponCode = request.CouponCode.Trim().ToUpperInvariant();
            await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Coupons\" WHERE \"Code\" = {couponCode} FOR UPDATE");
            var couponVal = await couponService.ValidateCouponAsync(request.CouponCode, summary.Subtotal, delivery);
            if (!couponVal.IsValid) return new ShopError(couponVal.Message ?? "Coupon is no longer available.");
            if (couponVal.IsValid && !string.IsNullOrEmpty(couponVal.Code))
            {
                discount = couponVal.DiscountAmount;
                appliedCoupon = couponVal.Code;
                await couponService.RecordCouponUseAsync(appliedCoupon);
            }
        }

        var calculatedTotal = Math.Max(0, summary.Subtotal + delivery - discount);
        if (request.ExpectedTotal.HasValue && request.ExpectedTotal.Value != calculatedTotal)
        {
            return new ConflictError("Your cart, delivery charge, or coupon changed. Review your order again before placing it.");
        }
        var address = new DeliveryAddress
        {
            CustomerName = request.CustomerName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            FullAddress = request.Address.Trim(),
            Division = request.Division?.Trim() ?? "Dhaka",
            District = request.District.Trim(),
            Thana = request.Thana?.Trim() ?? request.City.Trim(),
            City = request.City.Trim(),
            CustomerId = userId
        };
        if (string.IsNullOrWhiteSpace(address.Email) && userId.HasValue)
        {
            var u = await db.Users.FindAsync(userId.Value);
            if (u != null) address.Email = u.Email;
        }

        var refCode = !string.IsNullOrWhiteSpace(request.ReferenceCode)
            ? request.ReferenceCode.Trim()
            : $"JAS{Random.Shared.Next(1000, 9999)}";

        // Extract Meta Ads and marketing attribution
        var httpContext = httpContextAccessor?.HttpContext;
        var attr = httpContext?.Items[MetaAttributionMiddleware.AttributionItemKey] as MetaAttributionDto;
        var clientIp = (httpContext?.Items[MetaAttributionMiddleware.ClientIpItemKey] as string)
            ?? MetaConversionsApiService.ResolveClientIp(httpContext ?? new DefaultHttpContext());
        var clientUserAgent = (httpContext?.Items[MetaAttributionMiddleware.ClientUserAgentItemKey] as string)
            ?? httpContext?.Request.Headers.UserAgent.ToString();

        var fbclid = !string.IsNullOrWhiteSpace(request.Fbclid) ? request.Fbclid.Trim() : attr?.Fbclid;
        var fbp = !string.IsNullOrWhiteSpace(request.Fbp) ? request.Fbp.Trim() : attr?.Fbp;
        var fbc = !string.IsNullOrWhiteSpace(request.Fbc) ? request.Fbc.Trim() : attr?.Fbc;
        var utmSource = !string.IsNullOrWhiteSpace(request.UtmSource) ? request.UtmSource.Trim() : attr?.UtmSource;
        var utmMedium = !string.IsNullOrWhiteSpace(request.UtmMedium) ? request.UtmMedium.Trim() : attr?.UtmMedium;
        var utmCampaign = !string.IsNullOrWhiteSpace(request.UtmCampaign) ? request.UtmCampaign.Trim() : attr?.UtmCampaign;
        var utmContent = !string.IsNullOrWhiteSpace(request.UtmContent) ? request.UtmContent.Trim() : attr?.UtmContent;
        var utmTerm = !string.IsNullOrWhiteSpace(request.UtmTerm) ? request.UtmTerm.Trim() : attr?.UtmTerm;
        var campaignId = !string.IsNullOrWhiteSpace(request.CampaignId) ? request.CampaignId.Trim() : attr?.CampaignId;
        var adSetId = !string.IsNullOrWhiteSpace(request.AdSetId) ? request.AdSetId.Trim() : attr?.AdSetId;
        var adId = !string.IsNullOrWhiteSpace(request.AdId) ? request.AdId.Trim() : attr?.AdId;
        var landingPath = attr?.LandingPath;
        var marketingConsent = httpContext?.Request.Cookies["jas_cookie_consent"] != "essential";
        if (!marketingConsent)
        {
            fbclid = fbp = fbc = utmSource = utmMedium = utmCampaign = utmContent = utmTerm = campaignId = adSetId = adId = landingPath = null;
            clientIp = clientUserAgent = null;
        }
        var hasFbclid = !string.IsNullOrWhiteSpace(fbclid);
        var firstTouch = attr?.FirstTouchSource ?? (hasFbclid ? "meta_ads" : (!string.IsNullOrWhiteSpace(utmSource) ? utmSource : "direct"));
        var lastTouch = attr?.LastTouchSource ?? (!string.IsNullOrWhiteSpace(utmSource) ? utmSource : (hasFbclid ? "meta_ads" : "direct"));

        var order = new Order
        {
            UserId = userId,
            CheckoutToken = request.RequestId,
            Status = OrderStatus.Pending,
            DeliveryAddressId = 0,
            DeliveryAddress = address,
            Subtotal = summary.Subtotal,
            DeliveryCharge = delivery,
            DiscountAmount = discount,
            CouponCode = appliedCoupon,
            Total = calculatedTotal,
            PaymentReferenceCode = refCode,
            SenderPhoneNumber = request.SenderPhoneNumber?.Trim(),
            TransactionId = request.TransactionId?.Trim().ToUpperInvariant(),
            PaidAmount = 0m,
            Fbclid = fbclid,
            Fbp = fbp,
            Fbc = fbc,
            UtmSource = utmSource,
            UtmMedium = utmMedium,
            UtmCampaign = utmCampaign,
            UtmContent = utmContent,
            UtmTerm = utmTerm,
            FirstTouchSource = firstTouch,
            LastTouchSource = lastTouch,
            CampaignId = campaignId,
            AdSetId = adSetId,
            AdId = adId,
            LandingPath = landingPath,
            MarketingConsent = httpContext?.Request.Cookies["jas_cookie_consent"] != "essential",
            MetaPurchaseSent = httpContext?.Request.Cookies["jas_cookie_consent"] != "essential",
            ClientIpAddress = clientIp,
            ClientUserAgent = clientUserAgent,
            Items = [.. summary.Items.Select(i => new OrderItem
                {
                    ProductVariantId = i.VariantId,
                    ProductVariant = db.ProductVariants.Local.FirstOrDefault(v => v.Id == i.VariantId),
                    ProductName = i.Name,
                    VariantName = i.Variant,
                    UnitPrice = i.Price,
                    UnitCost = unitCosts[i.VariantId],
                    Quantity = i.Quantity,
                    LineTotal = i.LineTotal
                })]
        };
        order.Payments.Add(new Payment
        {
            OrderId = 0,
            Method = request.PaymentMethod == "BkashAdvance" || request.PaymentMethod == "FullPayment" ? PaymentMethod.Bkash : PaymentMethod.CashOnDelivery,
            Status = PaymentStatus.Pending,
            Amount = 0m,
            ExternalReference = request.TransactionId?.Trim().ToUpperInvariant()
        });
        db.Orders.Add(order);
        db.OrderTimelineEvents.Add(new OrderTimelineEvent
        {
            Order = order,
            Title = "Order Placed",
            Description = $"Order placed for ৳{order.Total:N0} via {request.PaymentMethod}. Awaiting review & fulfillment.",
            Actor = "Customer",
            BadgeColor = "blue"
        });
        if (cart is not null)
        {
            cart.Status = CartStatus.Converted;
            db.CartItems.RemoveRange(cart.Items);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();

        // Dispatch Meta Conversions API (CAPI) server-side Purchase event
        if (metaApi != null)
        {
            try
            {
                order.MetaEventId = order.PublicToken.ToString();
                await metaApi.EnqueuePurchaseEventAsync(order, request, httpContext);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to send Meta Conversions API purchase event for Order #{OrderId}", order.Id);
            }
        }

        // Dispatch order placed email
        try
        {
            await mailService.SendOrderPlacedEmailAsync(order);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send order placed email: {ex.Message}");
        }

        return new CheckoutResult(order.Id, order.PublicToken, order.Total, order.PaymentReferenceCode, order.DeliveryCharge, order.DiscountAmount);
    }

    public async Task<Result<Order>> ConfirmPaymentAsync(Guid publicToken, ConfirmPaymentRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Orders\" WHERE \"PublicToken\" = {publicToken} FOR UPDATE");
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.PublicToken == publicToken);
        if (order is null)
        {
            return new NotFoundError("Order not found.");
        }
        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            return new ConflictError($"This order cannot be updated because its status is {order.Status}.");
        }

        await db.Entry(order).ReloadAsync();
        if (order.PaidAmount > 0) return new ConflictError("This payment is already verified.");
        if (request.PaymentMethod is not ("COD" or "BkashAdvance" or "FullPayment"))
            return new ShopError("Select a supported payment method.");
        var dSettings = await deliverySettings.GetSettingsAsync();

        if (request.PaymentMethod is "BkashAdvance" or "FullPayment")
        {
            if (string.IsNullOrWhiteSpace(request.SenderPhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(request.SenderPhoneNumber.Trim(), @"^(?:\+?88)?01[3-9]\d{8}$"))
            {
                return new ValidationError("A valid 11-digit bKash sender phone number is required (01xxxxxxxxx).");
            }
            if (string.IsNullOrWhiteSpace(request.TransactionId) || request.TransactionId.Trim().Length < 6)
            {
                return new ValidationError("A valid bKash Transaction ID (TrxID) is required.");
            }

            var isFull = request.PaymentMethod == "FullPayment";
            var payableAmt = isFull ? order.Total : dSettings.AdvanceAmount;

            if (!string.IsNullOrWhiteSpace(request.ReferenceCode))
            {
                order.PaymentReferenceCode = request.ReferenceCode.Trim();
            }
            order.SenderPhoneNumber = request.SenderPhoneNumber.Trim();
            order.TransactionId = request.TransactionId.Trim().ToUpperInvariant();
            order.PaidAmount = 0m; // Verification pending!
            // Fulfillment is independent of payment submission.

            var existingPayment = order.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Bkash);
            if (existingPayment != null)
            {
                existingPayment.Amount = payableAmt;
                existingPayment.Status = PaymentStatus.Pending; // Pending verification!
                existingPayment.ExternalReference = order.TransactionId;
            }
            else
            {
                order.Payments.Add(new Payment
                {
                    OrderId = order.Id,
                    Method = PaymentMethod.Bkash,
                    Status = PaymentStatus.Pending,
                    Amount = payableAmt,
                    ExternalReference = order.TransactionId
                });
            }
        }
        else if (request.PaymentMethod == "COD")
        {
            if (dSettings.RequireAdvanceForCOD)
            {
                return new ShopError("Advance delivery fee is required for cash on delivery orders.");
            }
            order.PaidAmount = 0m;
            // Preserve fulfillment progress.
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();
        return order;
    }

    public async Task<Result<Order>> TryVerifyPaymentAsync(int orderId, decimal? verifiedAmount = null, string? verifiedTrxId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var transactionId = verifiedTrxId?.Trim().ToUpperInvariant();
        // Serialize all verifications. This also protects legacy rows without a transaction ID.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({739172004L})");
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE");
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return Error.NotFound("Order not found.");
        }

        await db.Entry(order).ReloadAsync();
        foreach (var payment in order.Payments) await db.Entry(payment).ReloadAsync();
        if (order.Status == OrderStatus.Cancelled) return Error.Conflict("Cancelled orders cannot accept payment.");
        var dSettings = await deliverySettings.GetSettingsAsync();
        var amount = verifiedAmount ?? order.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Bkash)?.Amount ?? dSettings.AdvanceAmount;
        if (amount <= 0) amount = verifiedAmount ?? dSettings.AdvanceAmount;
        transactionId ??= order.TransactionId?.Trim().ToUpperInvariant();
        if (amount <= 0 || amount > order.Total) return Error.Validation("Payment amount must be positive and cannot exceed the order total.");
        if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Length < 6)
            return Error.Validation("A valid transaction ID is required for verification.");
        var completed = order.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Bkash && p.Status == PaymentStatus.Completed);
        if (completed != null)
        {
            if (completed.ExternalReference == transactionId && completed.Amount == amount)
            {
                await tx.CommitAsync();
                return order;
            }
            return Error.Conflict("This order already has a verified payment. Review it manually.");
        }
        if (await db.Payments.AnyAsync(p => p.OrderId != orderId && p.Method == PaymentMethod.Bkash
            && p.Status == PaymentStatus.Completed && p.ExternalReference == transactionId))
            return Error.Conflict("This transaction has already been verified against another order.");
        order.TransactionId = transactionId;
        order.PaidAmount = amount;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        db.OrderTimelineEvents.Add(new OrderTimelineEvent
        {
            OrderId = order.Id,
            Title = "Payment Verified",
            Description = $"Verified payment of ৳{amount:N0} (TrxID: {order.TransactionId ?? "N/A"}).",
            Actor = "Admin / Cashier",
            BadgeColor = "emerald"
        });

        var bkashPayment = order.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Bkash);
        if (bkashPayment != null)
        {
            bkashPayment.Amount = amount;
            bkashPayment.Status = PaymentStatus.Completed;
            bkashPayment.ExternalReference = order.TransactionId;
        }
        else
        {
            order.Payments.Add(new Payment
            {
                OrderId = order.Id,
                Method = PaymentMethod.Bkash,
                Status = PaymentStatus.Completed,
                Amount = amount,
                ExternalReference = order.TransactionId
            });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();

        // Send payment verified email with full order summary
        try
        {
            await mailService.SendPaymentVerifiedEmailAsync(order);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send payment verified email: {ex.Message}");
        }

        return order;
    }

    public Task<Result<Order>> VerifyPaymentAsync(int orderId, decimal? verifiedAmount = null, string? verifiedTrxId = null)
        => TryVerifyPaymentAsync(orderId, verifiedAmount, verifiedTrxId);

    public async Task<Result<Order>> UnverifyPaymentAsync(int orderId)
    {
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return new NotFoundError("Order not found.");
        }

        order.PaidAmount = 0m;
        foreach (var p in order.Payments.Where(p => p.Method == PaymentMethod.Bkash))
        {
            p.Status = PaymentStatus.Pending;
        }

        db.OrderTimelineEvents.Add(new OrderTimelineEvent
        {
            OrderId = order.Id,
            Title = "Payment Marked Unverified",
            Description = "Payment status was reset to unverified by admin.",
            Actor = "Admin",
            BadgeColor = "amber"
        });

        await db.SaveChangesAsync();
        return order;
    }

    public async Task<Result<Order>> RejectPaymentAndCancelOrderAsync(int orderId, string? reason = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE");
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return new NotFoundError("Order not found.");
        }

        await db.Entry(order).ReloadAsync();
        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            return new ConflictError($"Order #{orderId} is already {order.Status}.");
        }


        // Release reserved inventory back to stock
        foreach (var item in order.Items.OrderBy(i => i.ProductVariantId))
        {
            await db.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {item.ProductVariantId} FOR UPDATE");
            var stock = await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == item.ProductVariantId);
            if (stock != null && stock.QuantityReserved >= item.Quantity)
            {
                stock.QuantityReserved -= item.Quantity;
            }
        }

        order.PaidAmount = 0m;
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        db.OrderTimelineEvents.Add(new OrderTimelineEvent
        {
            OrderId = order.Id,
            Title = "Order Cancelled (Payment Not Received)",
            Description = reason ?? "Payment was rejected and reserved inventory released back to stock.",
            Actor = "Admin Staff",
            BadgeColor = "rose"
        });

        foreach (var p in order.Payments)
        {
            p.Status = PaymentStatus.Failed;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (catalog != null) await catalog.InvalidateProductCacheAsync();

        // Dispatch cancellation email to customer
        try
        {
            await mailService.SendOrderStatusUpdateEmailAsync(order, OrderStatus.Cancelled);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send cancellation email: {ex.Message}");
        }

        return order;
    }
}
