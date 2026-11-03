using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace JASIntWeb.Services;

public sealed class EmailVerificationService(
    IMemoryCache cache,
    AccountMailService mailService,
    ILogger<EmailVerificationService> logger,
    IWebHostEnvironment env)
{
    private static string CacheKey(string email) => $"email_verify:{email.Trim().ToLowerInvariant()}";

    public async Task<string> GenerateAndSendCodeAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        // Generate a 6-digit numeric verification code (100000 - 999999)
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        // Store in memory cache for 15 minutes
        cache.Set(CacheKey(normalizedEmail), code, TimeSpan.FromMinutes(15));

        logger.LogInformation("[EmailVerification] Generated verification code for {Email}", normalizedEmail);

        try
        {
            var sent = await mailService.SendVerificationCodeEmailAsync(normalizedEmail, code);
            if (!sent)
            {
                logger.LogWarning("[EmailVerification] Email sending returned false (SMTP not configured or failed). Dev code for {Email} is: {Code}", normalizedEmail, code);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[EmailVerification] Error sending verification email to {Email}", normalizedEmail);
        }

        return code;
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
        if (env.IsDevelopment() || !mailService.IsConfigured)
        {
            if (cache.TryGetValue(CacheKey(email), out string? code))
            {
                return code;
            }
        }
        return null;
    }
}
