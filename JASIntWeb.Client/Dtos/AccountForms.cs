using System.ComponentModel.DataAnnotations;
namespace JASIntWeb.Client.Dtos;

public sealed class LoginForm
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
    public bool Remember { get; set; }
}

public sealed class SignupForm
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, MinLength(8), StringLength(128)]
    public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class EmailForm
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
}

public sealed class ResetPasswordForm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required]
    public string Code { get; set; } = "";
    [Required, MinLength(8), StringLength(128)]
    public string Password { get; set; } = "";
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}
