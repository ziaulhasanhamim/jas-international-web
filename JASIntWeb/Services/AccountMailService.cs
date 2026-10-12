using System.Net;
using System.Net.Mail;
namespace JASIntWeb.Services;

public sealed class AccountMailService(IConfiguration config, ILogger<AccountMailService> log)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Mail:Host"]) && !string.IsNullOrWhiteSpace(config["Mail:From"]);

    public async Task<bool> SendAsync(string to, string subject, string text)
    {
        if (!IsConfigured)
        {
            return false;
        }
        try
        {
            using var client = new SmtpClient(config["Mail:Host"], config.GetValue<int>("Mail:Port", 587))
            {
                EnableSsl = config.GetValue<bool>("Mail:EnableSsl", true)
            };
            if (!string.IsNullOrWhiteSpace(config["Mail:Username"]))
            {
                client.Credentials = new NetworkCredential(config["Mail:Username"], config["Mail:Password"]);
            }
            using var message = new MailMessage(config["Mail:From"]!, to, subject, text);
            await client.SendMailAsync(message);
            return true;
        }
        catch (SmtpException e)
        {
            log.LogWarning("Account email failed: {Status}", e.StatusCode);
            return false;
        }
    }

    public string Link(string path, HttpRequest request)
    {
        var origin = config["PublicBaseUrl"]?.TrimEnd('/') ?? $"{request.Scheme}://{request.Host}";
        return origin + path;
    }
}
