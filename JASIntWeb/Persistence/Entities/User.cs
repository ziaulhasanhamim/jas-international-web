using Microsoft.AspNetCore.Identity;
namespace JASIntWeb.Persistence.Entities;

public sealed class User : IdentityUser<int>
{
    public ICollection<DeliveryAddress> DeliveryAddresses { get; set; } = [];
}
