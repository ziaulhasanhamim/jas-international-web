using JASIntWeb.Client.Models;

namespace JASIntWeb.Client.Services;

public sealed class CartService
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items;

    public int ItemCount => _items.Sum(x => x.Quantity);

    public decimal Total =>
        _items.Sum(x => x.Product.Price * x.Quantity);

    public event Action? OnChange;

    public void Add(Product product)
    {
        var existing = _items.FirstOrDefault(
            x => x.Product.Name == product.Name);

        if (existing is not null)
        {
            existing.Quantity++;
        }
        else
        {
            _items.Add(new CartItem(product, 1));
        }

        Notify();
    }

    public void Remove(Product product)
    {
        var item = _items.FirstOrDefault(
            x => x.Product.Name == product.Name);

        if (item is null)
            return;

        _items.Remove(item);

        Notify();
    }

    public void Increase(Product product)
    {
        var item = _items.FirstOrDefault(
            x => x.Product.Name == product.Name);

        if (item is null)
            return;

        item.Quantity++;

        Notify();
    }

    public void Decrease(Product product)
    {
        var item = _items.FirstOrDefault(
            x => x.Product.Name == product.Name);

        if (item is null)
            return;

        item.Quantity--;

        if (item.Quantity <= 0)
            _items.Remove(item);

        Notify();
    }

    public void Clear()
    {
        _items.Clear();
        Notify();
    }

    private void Notify()
    {
        OnChange?.Invoke();
    }
}

public sealed class CartItem(Product product, int quantity)
{
    public Product Product { get; } = product;

    public int Quantity { get; set; } = quantity;
}