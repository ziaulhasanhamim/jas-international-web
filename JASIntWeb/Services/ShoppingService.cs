using System.Data;
using System.Security.Claims;
namespace JASIntWeb.Services;

public sealed class ShopException(string message) : Exception(message);

public sealed class ShoppingService(AppDbContext db, IConfiguration config)
{
    public static int? UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public DeliveryRatesDto Rates => new(config.GetValue<decimal>("Delivery:Khulna", 80), config.GetValue<decimal>("Delivery:OutsideKhulna", 130));

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
            request.Items = cart.Items.Select(i => new CartLineRequest(i.ProductVariantId, i.Quantity)).ToList();
        }
        if (request.Items.Count == 0)
        {
            throw new ShopException("Your cart is empty.");
        }
        var summary = await ResolveAsync(request.Items, userId.HasValue);
        if (summary.Items.Any(i => !i.IsAvailable))
        {
            throw new ShopException("One or more cart items are unavailable. Update your cart before checking out.");
        }
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
            summary.Items[summary.Items.IndexOf(item)] = item with
            {
                Price = variant.SellingPrice
            };
        }
        var delivery = string.Equals(request.District.Trim(), "Khulna", StringComparison.OrdinalIgnoreCase) ? Rates.Khulna : Rates.OutsideKhulna;
        if (request.ExpectedTotal.HasValue && request.ExpectedTotal.Value != summary.Subtotal + delivery)
        {
            throw new ShopException("Your cart or delivery charge changed. Review your order again before placing it.");
        }
        var address = new DeliveryAddress
        {
            CustomerName = request.CustomerName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            FullAddress = request.Address.Trim(),
            District = request.District.Trim(),
            City = request.City.Trim(),
            CustomerId = userId
        };
        var order = new Order
        {
            UserId = userId,
            CheckoutToken = request.RequestId,
            Status = OrderStatus.Pending,
            DeliveryAddressId = 0,
            DeliveryAddress = address,
            Subtotal = summary.Subtotal,
            DeliveryCharge = delivery,
            Total = summary.Subtotal + delivery,
            Items = summary.Items.Select(i => new OrderItem
                {
                    ProductVariantId = i.VariantId,
                    ProductName = i.Name,
                    VariantName = i.Variant,
                    UnitPrice = i.Price,
                    Quantity = i.Quantity,
                    LineTotal = i.LineTotal
                })
                .ToList()
        };
        order.Payments.Add(new Payment
            {
                OrderId = 0,
                Method = PaymentMethod.CashOnDelivery,
                Status = PaymentStatus.Pending,
                Amount = order.Total
            });
        db.Orders.Add(order);
        if (cart is not null)
        {
            cart.Status = CartStatus.Converted;
            db.CartItems.RemoveRange(cart.Items);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(order.Id, order.PublicToken, order.Total);
    }
}
