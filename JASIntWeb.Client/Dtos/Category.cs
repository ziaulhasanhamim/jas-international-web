namespace JASIntWeb.Client.Dtos;

public sealed record CategoryDto(
    string Name,
    string? Icon,
    string NormalizedName
)
{
    public string Href => $"/category/{NormalizedName}";
    public bool IsIconUrl => Icon is not null && Icon.Contains('/');
}
