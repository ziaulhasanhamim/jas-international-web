namespace JASIntWeb.Persistence.Entities;

public sealed class UrlRedirect : Entity
{
    public required string OldPath { get; set; }
    public required string NewPath { get; set; }
    public int StatusCode { get; set; } = 301;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
