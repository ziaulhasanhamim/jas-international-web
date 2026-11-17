using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JASIntWeb.Tests.Integration.Infrastructure;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAuth";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return AuthenticateResult.NoResult();
        }

        var userId = Context.Request.Headers["X-Test-UserId"].FirstOrDefault();
        if (string.IsNullOrEmpty(userId))
        {
            return AuthenticateResult.NoResult();
        }

        var role = Context.Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Admin";
        var name = Context.Request.Headers["X-Test-Name"].FirstOrDefault() ?? "Admin";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };

        var db = Context.RequestServices.GetRequiredService<JASIntWeb.Persistence.AppDbContext>();
        var stamp = Context.Request.Headers["X-Test-SecurityStamp"].FirstOrDefault()
            ?? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.Users.Where(user => user.Id == int.Parse(userId)).Select(user => user.SecurityStamp));
        if (stamp is not null) claims.Add(new Claim("AspNet.Identity.SecurityStamp", stamp));
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = Context.Request.Path + Context.Request.QueryString;
        Response.Redirect($"/account/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    }
}
