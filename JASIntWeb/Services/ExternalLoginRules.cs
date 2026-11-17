using System.Security.Claims;

namespace JASIntWeb.Services;

public static class ExternalLoginRules
{
    public static bool HasVerifiedEmail(ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirstValue("email_verified"), out var verified) && verified;
}
