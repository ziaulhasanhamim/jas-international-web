using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed class EmailVerificationService(
    HybridCache cache,
    UserManager<User> users,
    IWebHostEnvironment environment,
    AccountMailService mailService,
    ILogger<EmailVerificationService> logger)
{
    private static string CacheKey(string email) => $"email_verify:{email.Trim().ToLowerInvariant()}";

    public bool IsConfigured => mailService.IsConfigured;

    public async Task<(string Code, bool Sent)> GenerateAndSendCodeAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(normalizedEmail);
        if (user is null || user.EmailConfirmed) return ("", false);
        // Identity derives the code from the shared account security stamp, so any instance can verify it.
        var code = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "Signup");
        if (environment.IsDevelopment() && !mailService.IsConfigured)
            await cache.SetAsync(CacheKey(normalizedEmail), code, new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(9),
                LocalCacheExpiration = TimeSpan.FromMinutes(9)
            });

        var sent = false;
        try
        {
            sent = await mailService.SendVerificationCodeEmailAsync(normalizedEmail, code);
            if (!sent)
            {
                logger.LogWarning("[EmailVerification] Email sending returned false. IsConfigured={IsConfigured}", mailService.IsConfigured);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[EmailVerification] Error sending verification email to {Email}", normalizedEmail);
        }

        return (code, sent);
    }

    public async Task<bool> ValidateCodeAsync(string email, string code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)) return false;
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.EmailConfirmed
            || !await users.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "Signup", code.Trim()))
            return false;

        // The caller holds the account lock and transaction; rotating the stamp consumes the code.
        var consumed = await users.UpdateSecurityStampAsync(user);
        if (!consumed.Succeeded) return false;
        await cache.RemoveAsync(CacheKey(email));
        return true;
    }

    public async Task<string?> GetDevCodeAsync(string email)
    {
        if (!environment.IsDevelopment() || mailService.IsConfigured) return null;
        return await cache.GetOrCreateAsync(CacheKey(email), _ => ValueTask.FromResult(""));
    }
}
