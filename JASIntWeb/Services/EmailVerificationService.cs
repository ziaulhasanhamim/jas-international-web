using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace JASIntWeb.Services;

public sealed class EmailVerificationService(
    IMemoryCache cache,
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

        // Store in memory cache for 15 minutes
        cache.Set(CacheKey(normalizedEmail), code, TimeSpan.FromMinutes(15));

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

    public bool ValidateCode(string email, string code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)) return false;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = CacheKey(normalizedEmail);

        if (cache.TryGetValue(key, out string? cachedCode) && !string.IsNullOrWhiteSpace(cachedCode))
        {
            if (string.Equals(cachedCode.Trim(), code.Trim(), StringComparison.Ordinal))
            {
                cache.Remove(key);
                return true;
            }
        }

        return false;
    }

    public string? GetDevCode(string email)
    {
        // Only show dev code on screen if SMTP is not configured
        if (!mailService.IsConfigured)
        {
            if (cache.TryGetValue(CacheKey(email), out string? code))
            {
                return code;
            }
        }
        return null;
    }
}
