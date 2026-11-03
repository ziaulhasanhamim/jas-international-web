using System.Net;
using System.Net.Mail;
using System.Text;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Services;

public sealed class AccountMailService(IConfiguration config, ILogger<AccountMailService> log)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Mail:Host"]) && !string.IsNullOrWhiteSpace(config["Mail:From"]);

    public async Task<bool> SendAsync(string to, string subject, string text)
    {
        if (!IsConfigured)
        {
            log.LogInformation("SMTP not configured. Skipping plain text email to {To} with subject '{Subject}'.", to, subject);
            return false;
        }
        try
        {
            using var client = CreateClient();
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

    public async Task<bool> SendHtmlAsync(string to, string subject, string htmlBody)
    {
        if (!IsConfigured)
        {
            log.LogInformation("SMTP not configured. Skipping HTML email to {To} with subject '{Subject}'.", to, subject);
            return false;
        }
        try
        {
            using var client = CreateClient();
            using var message = new MailMessage(config["Mail:From"]!, to, subject, htmlBody)
            {
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8
            };
            await client.SendMailAsync(message);
            log.LogInformation("Email sent successfully to {To} for '{Subject}'", to, subject);
            return true;
        }
        catch (Exception e)
        {
            log.LogWarning("HTML email sending failed: {Message}", e.Message);
            return false;
        }
    }

    public async Task<bool> SendVerificationCodeEmailAsync(string email, string code)
    {
        var subject = $"[JAS International] Verification Code: {code}";
        var sb = new StringBuilder();
        sb.Append("<div style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:540px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:16px;overflow:hidden;'>");
        
        // Header
        sb.Append("<div style='background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:32px 24px;text-align:center;color:#ffffff;'>");
        sb.Append("<h1 style='margin:0;font-size:24px;font-weight:800;letter-spacing:-0.5px;'>JAS International</h1>");
        sb.Append("<p style='margin:8px 0 0;font-size:13px;color:#bfdbfe;font-weight:600;text-transform:uppercase;letter-spacing:1px;'>Email Verification</p>");
        sb.Append("</div>");

        // Body
        sb.Append("<div style='padding:32px 24px;color:#334155;font-size:14px;line-height:1.6;'>");
        sb.Append("<p style='margin:0 0 16px;font-size:15px;color:#0f172a;font-weight:600;'>Welcome to JAS International!</p>");
        sb.Append("<p style='margin:0 0 16px;'>Please use the 6-digit confirmation code below to verify your email address and activate your account:</p>");

        // Code Box
        sb.Append("<div style='text-align:center;margin:24px 0;padding:20px;background:#f8fafc;border-radius:12px;border:1px dashed #cbd5e1;'>");
        sb.Append($"<div style='font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,monospace;font-size:36px;font-weight:800;letter-spacing:8px;color:#2563eb;'>{System.Net.WebUtility.HtmlEncode(code)}</div>");
        sb.Append("<p style='margin:8px 0 0;font-size:12px;color:#64748b;'>This code is valid for 15 minutes.</p>");
        sb.Append("</div>");

        sb.Append("<p style='margin:0;font-size:13px;color:#64748b;'>If you did not request this verification code, you can safely ignore this email.</p>");
        sb.Append("</div>");

        // Footer
        sb.Append("<div style='background:#f8fafc;padding:16px 24px;text-align:center;font-size:12px;color:#94a3b8;border-top:1px solid #f1f5f9;'>");
        sb.Append("&copy; ").Append(DateTime.UtcNow.Year).Append(" JAS International. All rights reserved.");
        sb.Append("</div></div>");

        return await SendHtmlAsync(email, subject, sb.ToString());
    }

    public async Task<bool> SendPaymentVerifiedEmailAsync(Order order)
    {
        var recipientEmail = order.DeliveryAddress?.Email ?? order.User?.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            log.LogInformation("No customer email available for Order #{OrderId}. Skipping email notification.", order.Id);
            return false;
        }

        var baseUrl = config["PublicBaseUrl"]?.TrimEnd('/') ?? "https://jasinternational.com";
        var customerName = order.DeliveryAddress?.CustomerName ?? "Valued Customer";
        var subject = $"[JAS International] Payment Verified for Order #{order.Id} (Ref: {order.PaymentReferenceCode})";

        var sb = new StringBuilder();
        sb.Append("<div style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:16px;overflow:hidden;'>");
        
        // Header
        sb.Append("<div style='background:linear-gradient(135deg,#059669,#0d9488);padding:32px 24px;text-align:center;color:#ffffff;'>");
        sb.Append("<h1 style='margin:0;font-size:24px;font-weight:800;letter-spacing:-0.5px;'>JAS International</h1>");
        sb.Append("<p style='margin:8px 0 0;font-size:14px;color:#d1fae5;font-weight:600;text-transform:uppercase;letter-spacing:1px;'>Payment Verified &bull; Order In Review</p>");
        sb.Append("</div>");

        // Body
        sb.Append("<div style='padding:28px 24px;color:#334155;font-size:14px;line-height:1.6;'>");
        sb.Append($"<p style='margin:0 0 16px;font-size:16px;font-weight:700;color:#0f172a;'>Dear {System.Net.WebUtility.HtmlEncode(customerName)},</p>");
        sb.Append($"<p style='margin:0 0 16px;'>Great news! We have successfully verified your bKash payment of <strong style='color:#059669;'>৳{order.PaidAmount:N0}</strong> for <strong>Order #{order.Id}</strong>.</p>");

        // Callout Box
        sb.Append("<div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;padding:16px;margin:20px 0;'>");
        sb.Append("<div style='font-size:13px;color:#166534;'>");
        sb.Append($"<div><strong>bKash Transaction ID (TrxID):</strong> <span style='font-family:monospace;font-weight:700;'>{System.Net.WebUtility.HtmlEncode(order.TransactionId ?? "N/A")}</span></div>");
        sb.Append($"<div style='margin-top:4px;'><strong>Order Reference:</strong> <span style='font-family:monospace;font-weight:700;'>{System.Net.WebUtility.HtmlEncode(order.PaymentReferenceCode ?? "N/A")}</span></div>");
        sb.Append($"<div style='margin-top:4px;'><strong>Amount Verified:</strong> <strong style='color:#059669;'>৳{order.PaidAmount:N0}</strong></div>");
        sb.Append("<div style='margin-top:8px;font-size:12px;color:#15803d;'>Your order is currently <strong>Pending Admin Confirmation</strong>. Our fulfillment team is reviewing your item specs and packing details. You will receive further updates as soon as your parcel is handed over to the courier.</div>");
        sb.Append("</div></div>");

        // Items Table
        sb.Append("<h3 style='margin:24px 0 12px;font-size:15px;color:#0f172a;border-bottom:1px solid #f1f5f9;padding-bottom:8px;'>Order Summary</h3>");
        sb.Append("<table style='width:100%;border-collapse:collapse;font-size:13px;margin-bottom:16px;'>");
        foreach (var item in order.Items)
        {
            sb.Append("<tr>");
            sb.Append($"<td style='padding:8px 0;border-bottom:1px solid #f8fafc;'><strong style='color:#1e293b;'>{System.Net.WebUtility.HtmlEncode(item.ProductName)}</strong><br/><span style='font-size:11px;color:#64748b;'>{System.Net.WebUtility.HtmlEncode(item.VariantName)} &times; {item.Quantity}</span></td>");
            sb.Append($"<td style='padding:8px 0;border-bottom:1px solid #f8fafc;text-align:right;font-weight:700;color:#0f172a;'>৳{item.LineTotal:N0}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");

        // Totals
        sb.Append("<div style='background:#f8fafc;border-radius:10px;padding:12px 16px;font-size:13px;margin-bottom:24px;'>");
        sb.Append($"<div style='display:flex;justify-content:space-between;margin-bottom:4px;'><span>Subtotal:</span><strong>৳{order.Subtotal:N0}</strong></div>");
        sb.Append($"<div style='display:flex;justify-content:space-between;margin-bottom:4px;'><span>Delivery Fee:</span><strong>৳{order.DeliveryCharge:N0}</strong></div>");
        if (order.DiscountAmount > 0)
        {
            sb.Append($"<div style='display:flex;justify-content:space-between;margin-bottom:4px;color:#059669;'><span>Coupon Discount ({System.Net.WebUtility.HtmlEncode(order.CouponCode)}):</span><strong>-৳{order.DiscountAmount:N0}</strong></div>");
        }
        sb.Append($"<div style='display:flex;justify-content:space-between;font-size:15px;font-weight:800;color:#2563eb;border-top:1px solid #e2e8f0;padding-top:6px;margin-top:6px;'><span>Total Order Amount:</span><span>৳{order.Total:N0}</span></div>");
        sb.Append($"<div style='display:flex;justify-content:space-between;font-size:12px;color:#059669;margin-top:6px;'><span>Paid via bKash:</span><strong>৳{order.PaidAmount:N0}</strong></div>");
        var due = Math.Max(0, order.Total - order.PaidAmount);
        sb.Append($"<div style='display:flex;justify-content:space-between;font-size:12px;color:#64748b;'><span>Due on Delivery:</span><strong>৳{due:N0}</strong></div>");
        sb.Append("</div>");

        // Delivery Info
        sb.Append("<div style='font-size:12px;color:#64748b;margin-bottom:24px;border-top:1px solid #f1f5f9;padding-top:12px;'>");
        sb.Append($"<div><strong>Delivery Address:</strong> {System.Net.WebUtility.HtmlEncode(order.DeliveryAddress?.FullAddress)}, {System.Net.WebUtility.HtmlEncode(order.DeliveryAddress?.Thana)}, {System.Net.WebUtility.HtmlEncode(order.DeliveryAddress?.District)} ({System.Net.WebUtility.HtmlEncode(order.DeliveryAddress?.Division)})</div>");
        sb.Append($"<div style='margin-top:4px;'><strong>Contact Phone:</strong> {System.Net.WebUtility.HtmlEncode(order.DeliveryAddress?.PhoneNumber ?? "")}</div>");
        sb.Append("</div>");

        // CTA Button
        var orderUrl = $"{baseUrl}/order/{order.PublicToken}";
        sb.Append($"<div style='text-align:center;margin:28px 0;'><a href='{orderUrl}' style='display:inline-block;background:#2563eb;color:#ffffff;font-size:14px;font-weight:700;padding:12px 28px;border-radius:10px;text-decoration:none;'>View Live Order Status &rarr;</a></div>");

        sb.Append("</div>"); // Body

        // Footer
        sb.Append("<div style='background:#f8fafc;padding:20px;text-align:center;font-size:11px;color:#94a3b8;border-top:1px solid #e2e8f0;'>");
        sb.Append("<p style='margin:0 0 4px;'><strong>JAS International</strong> &bull; Authentic Tech Gadgets &bull; Official Warranty</p>");
        sb.Append("<p style='margin:0;'>Khulna Showroom: Shopping Complex 3rd Floor &bull; Hotline: 01711465078</p>");
        sb.Append("</div>");
        sb.Append("</div>");

        return await SendHtmlAsync(recipientEmail, subject, sb.ToString());
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(config["Mail:Host"], config.GetValue<int>("Mail:Port", 587))
        {
            EnableSsl = config.GetValue<bool>("Mail:EnableSsl", true)
        };
        if (!string.IsNullOrWhiteSpace(config["Mail:Username"]))
        {
            client.Credentials = new NetworkCredential(config["Mail:Username"], config["Mail:Password"]);
        }
        return client;
    }

    public string Link(string path, HttpRequest request)
    {
        var origin = config["PublicBaseUrl"]?.TrimEnd('/') ?? $"{request.Scheme}://{request.Host}";
        return origin + path;
    }
}
