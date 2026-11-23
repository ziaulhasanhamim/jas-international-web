using System.ComponentModel.DataAnnotations;

namespace JASIntWeb.Client.Dtos;

public sealed record TagDto(
    int Id,
    string Name,
    string Slug,
    bool IsHidden,
    string? BadgeColor = null,
    int SortOrder = 0,
    int ProductCount = 0)
{
    public static string GetBadgeClasses(string? color) => color?.ToLowerInvariant() switch
    {
        "emerald" or "green" => "bg-emerald-50 text-emerald-700 border-emerald-200",
        "amber" or "yellow" => "bg-amber-50 text-amber-700 border-amber-200",
        "rose" or "red" => "bg-rose-50 text-rose-700 border-rose-200",
        "purple" => "bg-purple-50 text-purple-700 border-purple-200",
        "indigo" => "bg-indigo-50 text-indigo-700 border-indigo-200",
        _ => "bg-blue-50 text-blue-700 border-blue-200"
    };
}

public sealed class TagFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tag name is required."), StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = "";

    [StringLength(100, ErrorMessage = "Slug cannot exceed 100 characters.")]
    public string? Slug { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public bool IsHidden { get; set; }

    [StringLength(50)]
    public string BadgeColor { get; set; } = "blue"; // emerald, amber, rose, blue, indigo, purple

    public int SortOrder { get; set; }
}
