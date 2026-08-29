namespace JASIntWeb.Client.Models;

public sealed record Product(
    string Name,
    string Category,
    decimal Price,
    string? Badge,
    string Image
);