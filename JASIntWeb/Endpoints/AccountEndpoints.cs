using System.Security.Claims;
using JASIntWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JASIntWeb.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/logout", Logout);
        endpoints.MapGet("/account/external-login", ExternalLogin);
        endpoints.MapGet("/account/external-login-callback", ExternalLoginCallback);
    }

    private static async Task<IResult> Logout(
        SignInManager<User> signIn, UserManager<User> users, HttpContext context, [FromForm] string? returnUrl)
    {
        if (await users.GetUserAsync(context.User) is { } user)
        {
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) return Results.Conflict(new EndpointError("Please try signing out again."));
        }
        await signIn.SignOutAsync();
        return Results.LocalRedirect(AccountNavigation.SafeReturn(returnUrl ?? "/"));
    }

    private static async Task<IResult> ExternalLogin(string provider, string? returnUrl, SignInManager<User> signIn)
    {
        var schemes = await signIn.GetExternalAuthenticationSchemesAsync();
        if (provider != "Google" || !schemes.Any(scheme => scheme.Name == provider))
            return Results.BadRequest(new EndpointError("This sign-in provider is unavailable."));

        var target = AccountNavigation.SafeReturn(returnUrl ?? "/");
        var callback = $"/account/external-login-callback?returnUrl={Uri.EscapeDataString(target)}";
        return Results.Challenge(signIn.ConfigureExternalAuthenticationProperties(provider, callback), [provider]);
    }

    private static async Task<IResult> ExternalLoginCallback(
        string? returnUrl, string? remoteError, SignInManager<User> signIn, UserManager<User> users, ILoggerFactory logs)
    {
        var logger = logs.CreateLogger(nameof(AccountEndpoints));
        var target = AccountNavigation.SafeReturn(returnUrl ?? "/");
        if (!string.IsNullOrEmpty(remoteError))
        {
            logger.LogWarning("External authentication was rejected by the provider.");
            return LoginError("Google sign-in could not be completed. Please try again.");
        }

        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null || info.LoginProvider != "Google")
            return LoginError("Sign-in details have expired. Please try again.");

        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
            isPersistent: true, bypassTwoFactor: false);
        if (result.Succeeded) return Results.LocalRedirect(target);
        if (result.IsLockedOut) return Results.LocalRedirect("/account/access-denied");
        if (result.RequiresTwoFactor) return LoginError("Use account sign-in to complete additional verification.");

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email) || !ExternalLoginRules.HasVerifiedEmail(info.Principal))
            return LoginError("Google must verify your email address before it can be linked to an account.");

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User { UserName = email, Email = email, EmailConfirmed = true };
            if (!(await users.CreateAsync(user)).Succeeded) return LoginError("The account could not be created. Please try again.");
            if (info.Principal.FindFirstValue(ClaimTypes.Name) is { Length: > 0 } name)
                await users.AddClaimAsync(user, new Claim("name", name));
        }
        else
        {
            if (await users.IsLockedOutAsync(user)) return Results.LocalRedirect("/account/access-denied");
            if (await users.GetTwoFactorEnabledAsync(user)) return LoginError("Use account sign-in to complete additional verification.");
            if (!user.EmailConfirmed)
                return LoginError("Complete email sign-up and verification before linking Google to this account.");
        }

        if (!(await users.AddLoginAsync(user, info)).Succeeded)
            return LoginError("This Google login could not be linked. Sign in with your existing account.");
        if (!await signIn.CanSignInAsync(user)) return LoginError("This account cannot sign in. Contact the store for assistance.");

        await signIn.SignInAsync(user, isPersistent: true, info.LoginProvider);
        return Results.LocalRedirect(target);
    }

    private static IResult LoginError(string message) =>
        Results.LocalRedirect($"/account/login?error={Uri.EscapeDataString(message)}");
}
