namespace JASIntWeb.Persistence.Entities;

public class DeliveryAddress
{
    public int Id { get; set; }
    public string FullAddress { get; set; } = null!;
    public string? Division { get; set; }
    public required string District { get; set; }
    public string? Thana { get; set; }
    public required string City { get; set; }
    public required string PhoneNumber { get; set; }
    public required string CustomerName { get; set; }
    public string? Email { get; set; }
    public int? CustomerId { get; set; }
}
