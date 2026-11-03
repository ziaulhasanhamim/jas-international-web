using System.Data;
using System.Security.Claims;
namespace JASIntWeb.Services;

public sealed class ShopException(string message) : Exception(message);

public sealed class ShoppingService(AppDbContext db, IConfiguration config, DeliverySettingsService deliverySettings, CouponService couponService, AccountMailService mailService)
{
    public static int? UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public DeliveryRatesDto Rates => new(config.GetValue<decimal>("Delivery:Khulna", 50), config.GetValue<decimal>("Delivery:OutsideKhulna", 150));

    public async Task LockUserAsync(int userId) => await db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE");

    private async Task<Cart> ActiveCartAsync(int userId)
    {
        var cart = await db.Carts.Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.UserId == userId && c.Status == CartStatus.Active);
        if (cart is not null)
        {
            return cart;
        }
        cart = new Cart
        {
            UserId = userId
        };
        db.Carts.Add(cart);
        return cart;
    }

    public async Task<CartDto> ResolveAsync(List<CartLineRequest> lines, bool authenticated = false)
    {
        if (lines.Count > 100)
        {
            throw new ShopException("A cart can contain at most 100 variants.");
        }
        if (lines.Any(l => l.VariantId <= 0 || l.Quantity <= 0 || l.Quantity > 99))
        {
            throw new ShopException("Quantity must be between 1 and 99.");
        }
        var normalized = lines.GroupBy(l => l.VariantId).Select(g =>
            {
                var total = g.Sum(l => l.Quantity);
                if (total > 99)
                {
                    throw new ShopException("Quantity must be between 1 and 99.");
                }
                return new CartLineRequest(g.Key, total);
            })
            .ToList();
        if (normalized.Count == 0)
        {
            return new(authenticated, []);
        }
        var ids = normalized.Select(l => l.VariantId).ToArray();
        var variants = await db.ProductVariants.AsNoTracking().Where(v => ids.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
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
                .Select(o => o.ProductAttributeOption!.AttributeOption!.Value)
                .ToList()
            })
            .ToDictionaryAsync(v => v.Id);
        return new(authenticated, normalized.Select(l =>
                {
                    var v = variants.GetValueOrDefault(l.VariantId);
                    var available = v?.Available == true;
                    return new CartItemDto(l.VariantId, v?.Name ?? "Unavailable product", v?.Slug ?? "",
                        v?.MainImageUrl ?? "", v is null ? "" : string.Join(" / ", v.Options),
                        v?.SellingPrice ?? 0, l.Quantity, available ? v!.Stock : 0, available);
                })
            .ToList());
    }

    public async Task<CartDto> GetCartAsync(int userId)
    {
        var lines = await db.CartItems.AsNoTracking()
            .Where(i => i.Cart.UserId == userId && i.Cart.Status == CartStatus.Active)
            .Select(i => new CartLineRequest(i.ProductVariantId, i.Quantity))
            .ToListAsync();
        return await ResolveAsync(lines, true);
    }

    public async Task<CartDto> ChangeAsync(int userId, CartLineRequest line, bool add)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockUserAsync(userId);
        var cart = await ActiveCartAsync(userId);
        var item = cart.Items.FirstOrDefault(i => i.ProductVariantId == line.VariantId);
        var quantity = add ? (item?.Quantity ?? 0) + line.Quantity : line.Quantity;
        if (quantity < 0 || quantity > 99 || (add && line.Quantity <= 0))
        {
            throw new ShopException("Quantity must be between 1 and 99.");
        }
        if (quantity == 0)
        {
            if (item is not null)
            {
                db.CartItems.Remove(item);
            }
        }
        else
        {
            var resolved = await ResolveAsync([new(line.VariantId, quantity)]);
            if (!resolved.Items[0].IsAvailable || resolved.Items[0].Stock < quantity)
            {
                throw new ShopException("The requested quantity is unavailable.");
            }
            if (item is null)
            {
                if (cart.Items.Count >= 100)
                {
                    throw new ShopException("A cart can contain at most 100 variants.");
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
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetCartAsync(userId);
    }

    public async Task<CartDto> MergeAsync(int userId, CartMergeRequest request)
    {
        if (request.Token == Guid.Empty)
        {
            throw new ShopException("The cart merge token is missing.");
        }
        var resolved = await ResolveAsync(request.Items);
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
                        throw new ShopException("A cart can contain at most 100 variants.");
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
        return await GetCartAsync(userId);
    }

    public async Task ClearAsync(int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockUserAsync(userId);
        var cart = await ActiveCartAsync(userId);
        db.CartItems.RemoveRange(cart.Items);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<CheckoutResult> CheckoutAsync(int? userId, CheckoutRequest request)
    {
        var validation = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, new System.ComponentModel.DataAnnotations.ValidationContext(request), validation, true))
        {
            throw new ShopException(string.Join(" ", validation.Select(e => e.ErrorMessage)));
        }
        if (request.RequestId == Guid.Empty)
        {
            throw new ShopException("The checkout token is missing.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        // One request token cannot create two orders, including guest retries and concurrent requests.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({request.RequestId.ToString()}, 0))");
        var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.CheckoutToken == request.RequestId);
        if (existing is not null)
        {
            if (existing.UserId != userId)
            {
                throw new ShopException("This checkout token has already been used.");
            }
            await tx.CommitAsync();
            return new(existing.Id, existing.PublicToken, existing.Total);
        }
        Cart? cart = null;
        if (userId.HasValue)
        {
            await LockUserAsync(userId.Value);
            cart = await ActiveCartAsync(userId.Value);
            if (cart.Items.Count == 0 && request.Items.Count > 0)
            {
                foreach (var line in request.Items)
                {
                    cart.Items.Add(new CartItem { ProductVariantId = line.VariantId, Quantity = line.Quantity });
                }
            }
            else
            {
                request.Items = cart.Items.Select(i => new CartLineRequest(i.ProductVariantId, i.Quantity)).ToList();
            }
        }
        if (request.Items.Count == 0)
        {
            throw new ShopException("Your cart is empty.");
        }

        // Strict payment validation
        if (request.PaymentMethod is "BkashAdvance" or "FullPayment")
        {
            if (string.IsNullOrWhiteSpace(request.SenderPhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(request.SenderPhoneNumber.Trim(), @"^(?:\+?88)?01[3-9]\d{8}$"))
            {
                throw new ShopException("A valid 11-digit bKash sender phone number is required to confirm payment.");
            }
            if (string.IsNullOrWhiteSpace(request.TransactionId) || request.TransactionId.Trim().Length < 6)
            {
                throw new ShopException("A valid bKash Transaction ID (TrxID) is required to place your order.");
            }
        }
        else if (request.PaymentMethod == "COD")
        {
            var dSettings = await deliverySettings.GetSettingsAsync();
            if (dSettings.RequireAdvanceForCOD)
            {
                throw new ShopException("Advance delivery fee is required for cash on delivery orders.");
            }
        }
        var summary = await ResolveAsync(request.Items, userId.HasValue);
        if (summary.Items.Any(i => !i.IsAvailable))
        {
            throw new ShopException("One or more cart items are unavailable. Update your cart before checking out.");
        }
        var unitCosts = new Dictionary<int, decimal>();
        foreach (var item in summary.Items.OrderBy(i => i.VariantId))
        {
            // Lock inventory in a stable order. The available quantity is checked again after acquiring the lock.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {item.VariantId} FOR UPDATE");
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
                throw new ShopException($"{item.Name}: the selected quantity is no longer available.");
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
            var couponVal = await couponService.ValidateCouponAsync(request.CouponCode, summary.Subtotal, delivery);
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
            throw new ShopException("Your cart, delivery charge, or coupon changed. Review your order again before placing it.");
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

        var refCode = $"JAS{Random.Shared.Next(1000, 9999)}";
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
            Items = summary.Items.Select(i => new OrderItem
                {
                    ProductVariantId = i.VariantId,
                    ProductName = i.Name,
                    VariantName = i.Variant,
                    UnitPrice = i.Price,
                    UnitCost = unitCosts[i.VariantId],
                    Quantity = i.Quantity,
                    LineTotal = i.LineTotal
                })
                .ToList()
        };
        order.Payments.Add(new Payment
            {
                OrderId = 0,
                Method = request.PaymentMethod == "BkashAdvance" || request.PaymentMethod == "FullPayment" || request.PaymentMethod == "Pending" ? PaymentMethod.Bkash : PaymentMethod.CashOnDelivery,
                Status = PaymentStatus.Pending,
                Amount = 0m,
                ExternalReference = request.TransactionId
            });
        db.Orders.Add(order);
        if (cart is not null)
        {
            cart.Status = CartStatus.Converted;
            db.CartItems.RemoveRange(cart.Items);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        // Dispatch order placed email
        try
        {
            await mailService.SendOrderPlacedEmailAsync(order);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send order placed email: {ex.Message}");
        }

        return new(order.Id, order.PublicToken, order.Total, order.PaymentReferenceCode, order.DeliveryCharge, order.DiscountAmount);
    }

    public async Task<Order> ConfirmPaymentAsync(Guid publicToken, ConfirmPaymentRequest request)
    {
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.PublicToken == publicToken);
        if (order is null)
        {
            throw new ShopException("Order not found.");
        }
        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            throw new ShopException($"This order cannot be updated because its status is {order.Status}.");
        }

        var dSettings = await deliverySettings.GetSettingsAsync();

        if (request.PaymentMethod is "BkashAdvance" or "FullPayment")
        {
            if (string.IsNullOrWhiteSpace(request.SenderPhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(request.SenderPhoneNumber.Trim(), @"^(?:\+?88)?01[3-9]\d{8}$"))
            {
                throw new ShopException("A valid 11-digit bKash sender phone number is required (01xxxxxxxxx).");
            }
            if (string.IsNullOrWhiteSpace(request.TransactionId) || request.TransactionId.Trim().Length < 6)
            {
                throw new ShopException("A valid bKash Transaction ID (TrxID) is required.");
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
            order.Status = OrderStatus.Pending; // Order remains pending until admin confirmation!

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
                throw new ShopException("Advance delivery fee is required for cash on delivery orders.");
            }
            order.PaidAmount = 0m;
            order.Status = OrderStatus.Pending;
        }

        await db.SaveChangesAsync();
        return order;
    }

    public async Task<Order> VerifyPaymentAsync(int orderId, decimal? verifiedAmount = null, string? verifiedTrxId = null)
    {
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            throw new ShopException("Order not found.");
        }

        var dSettings = await deliverySettings.GetSettingsAsync();
        var amount = verifiedAmount ?? (order.PaidAmount > 0 ? order.PaidAmount : dSettings.AdvanceAmount);

        if (!string.IsNullOrEmpty(verifiedTrxId))
        {
            order.TransactionId = verifiedTrxId.Trim().ToUpperInvariant();
        }
        order.PaidAmount = amount;
        // Keep order.Status as Pending! Admin must explicitly confirm the order.
        order.Status = OrderStatus.Pending;

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

    public async Task<Order> UnverifyPaymentAsync(int orderId)
    {
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            throw new ShopException("Order not found.");
        }

        order.PaidAmount = 0m;
        foreach (var p in order.Payments.Where(p => p.Method == PaymentMethod.Bkash))
        {
            p.Status = PaymentStatus.Pending;
        }

        await db.SaveChangesAsync();
        return order;
    }

    public async Task<Order> RejectPaymentAndCancelOrderAsync(int orderId, string? reason = null)
    {
        var order = await db.Orders
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            throw new ShopException("Order not found.");
        }

        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            throw new ShopException($"Order #{orderId} is already {order.Status}.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        // Release reserved inventory back to stock
        foreach (var item in order.Items.OrderBy(i => i.ProductVariantId))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"InventoryItems\" WHERE \"ProductVariantId\" = {item.ProductVariantId} FOR UPDATE");
            var stock = await db.InventoryItems.SingleOrDefaultAsync(i => i.ProductVariantId == item.ProductVariantId);
            if (stock != null && stock.QuantityReserved >= item.Quantity)
            {
                stock.QuantityReserved -= item.Quantity;
            }
        }

        order.PaidAmount = 0m;
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var p in order.Payments)
        {
            p.Status = PaymentStatus.Failed;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

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
