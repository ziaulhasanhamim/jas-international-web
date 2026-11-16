using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Hybrid;

namespace JASIntWeb.Services;

public sealed class EmailVerificationService(
    HybridCache cache,
    IWebHostEnvironment environment,
    AccountMailService mailService,
    ILogger<EmailVerificationService> logger)
{
    private static string CacheKey(string email) => $"email_verify:{email.Trim().ToLowerInvariant()}";

    public bool IsConfigured => mailService.IsConfigured;

    public async Task<(string Code, bool Sent)> GenerateAndSendCodeAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        // Generate a 6-digit numeric verification code (100000 - 999999)
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        // Store through HybridCache for 15 minutes
        await cache.SetAsync(CacheKey(normalizedEmail), code, new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(15), LocalCacheExpiration = TimeSpan.FromMinutes(15) });

        logger.LogInformation("[EmailVerification] Generated verification code for {Email}", normalizedEmail);

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
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = CacheKey(normalizedEmail);

        var cachedCode = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(""));
        if (!string.IsNullOrWhiteSpace(cachedCode))
        {
            if (string.Equals(cachedCode.Trim(), code.Trim(), StringComparison.Ordinal))
            {
                await cache.RemoveAsync(key);
                return true;
            }
        }

        return false;
    }

    public async Task<string?> GetDevCodeAsync(string email)
    {
        if (!environment.IsDevelopment() || mailService.IsConfigured) return null;
        return await cache.GetOrCreateAsync(CacheKey(email), _ => ValueTask.FromResult(""));
    }
}
