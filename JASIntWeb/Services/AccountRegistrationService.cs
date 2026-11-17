using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace JASIntWeb.Services;

public sealed record VerificationRequired(string Email);
public sealed record RegistrationRejected(string Message);
public sealed record VerifiedAccount(User User);
public union RegistrationResult(VerificationRequired, RegistrationRejected);
public union VerificationResult(VerifiedAccount, RegistrationRejected);

public sealed class AccountRegistrationService(
    AppDbContext db, UserManager<User> users, EmailVerificationService verification, IWebHostEnvironment environment)
{
    public async Task<RegistrationResult> RegisterAsync(string email, string fullName, string password, CancellationToken ct = default)
    {
        if (!verification.IsConfigured && !environment.IsDevelopment())
            return new RegistrationRejected("Email sign-up is temporarily unavailable. Contact the store for assistance.");

        email = email.Trim();
        await using var transaction = await LockAccountAsync(email, ct);
        var user = await users.FindByEmailAsync(email);
        if (user?.EmailConfirmed == true)
            return new RegistrationRejected("An account with this email address already exists. Please sign in.");

        IdentityResult result;
        if (user is null)
        {
            user = new User { UserName = email, Email = email, EmailConfirmed = false };
            result = await users.CreateAsync(user, password);
        }
        else
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            result = await users.ResetPasswordAsync(user, token, password);
        }
        if (!result.Succeeded) return Rejected(result);

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var previousName = (await users.GetClaimsAsync(user)).FirstOrDefault(claim => claim.Type == "name");
            result = previousName is null
                ? await users.AddClaimAsync(user, new Claim("name", fullName.Trim()))
                : await users.ReplaceClaimAsync(user, previousName, new Claim("name", fullName.Trim()));
            if (!result.Succeeded) return Rejected(result);
        }

        var (_, sent) = await verification.GenerateAndSendCodeAsync(email);
        if (!sent && !environment.IsDevelopment())
            return new RegistrationRejected("We could not send your verification email. Please try again later.");
        await transaction.CommitAsync(ct);
        return new VerificationRequired(email);
    }

    public async Task<VerificationResult> VerifyAsync(string email, string code, CancellationToken ct = default)
    {
        await using var transaction = await LockAccountAsync(email, ct);
        var user = await users.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed || !await verification.ValidateCodeAsync(email, code))
            return new RegistrationRejected("Invalid or expired verification code. Please check the code or click Resend.");

        user.EmailConfirmed = true;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return Rejected(result);
        await transaction.CommitAsync(ct);
        return new VerifiedAccount(user);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> LockAccountAsync(string email, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = $"account:{users.NormalizeEmail(email.Trim())}";
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static RegistrationRejected Rejected(IdentityResult result) =>
        new(string.Join(" ", result.Errors.Select(error => error.Description)));
}
