using System.Net;
using System.Net.Mail;
using System.Text;
using JASIntWeb.Persistence.Entities;

namespace JASIntWeb.Services;

public sealed class AccountMailService(IConfiguration config, ILogger<AccountMailService> log, IWebHostEnvironment env)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Mail:Host"]) && !string.IsNullOrWhiteSpace(config["Mail:From"]);

    private MailAddress GetFromAddress()
    {
        var fromEmail = config["Mail:From"]!;
        var displayName = config["Mail:DisplayName"] ?? "JAS International";
        return new MailAddress(fromEmail, displayName);
    }

    private string? GetRecipientEmail(Order order)
    {
        var recipientEmail = order.DeliveryAddress?.Email ?? order.User?.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail) && env.IsDevelopment())
        {
            // In development, fallback to developer's inbox so order update emails can always be tested
            recipientEmail = config["Mail:Username"] ?? config["Mail:From"];
        }
        return recipientEmail;
    }

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
            using var message = new MailMessage(GetFromAddress(), new MailAddress(to))
            {
                Subject = subject,
                Body = text
            };
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
            using var message = new MailMessage(GetFromAddress(), new MailAddress(to))
            {
                Subject = subject,
                Body = htmlBody,
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

    public async Task<bool> SendOrderPlacedEmailAsync(Order order)
    {
        var recipientEmail = GetRecipientEmail(order);
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            log.LogInformation("No recipient email found for Order #{OrderId}. Skipping placed email.", order.Id);
            return false;
        }

        var baseUrl = config["PublicBaseUrl"]?.TrimEnd('/') ?? "https://jasinternational.com";
        var customerName = order.DeliveryAddress?.CustomerName ?? "Valued Customer";
        var subject = $"[JAS International] Order #{order.Id} Placed Successfully (Ref: {order.PaymentReferenceCode})";

        var sb = new StringBuilder();
        sb.Append("<div style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:16px;overflow:hidden;'>");

        // Header
        sb.Append("<div style='background:linear-gradient(135deg,#1e40af,#3b82f6);padding:32px 24px;text-align:center;color:#ffffff;'>");
        sb.Append("<h1 style='margin:0;font-size:24px;font-weight:800;letter-spacing:-0.5px;'>JAS International</h1>");
        sb.Append("<p style='margin:8px 0 0;font-size:13px;color:#dbeafe;font-weight:600;text-transform:uppercase;letter-spacing:1px;'>Order Received &bull; Pending Review</p>");
        sb.Append("</div>");

        // Body
        sb.Append("<div style='padding:28px 24px;color:#334155;font-size:14px;line-height:1.6;'>");
        sb.Append($"<p style='margin:0 0 16px;font-size:16px;font-weight:700;color:#0f172a;'>Dear {System.Net.WebUtility.HtmlEncode(customerName)},</p>");
        sb.Append($"<p style='margin:0 0 16px;'>Thank you for your purchase! We have received <strong>Order #{order.Id}</strong>. Our fulfillment team will review your order details shortly.</p>");

        // Callout
        sb.Append("<div style='background:#eff6ff;border:1px solid #bfdbfe;border-radius:12px;padding:16px;margin:20px 0;'>");
        sb.Append("<div style='font-size:13px;color:#1e40af;'>");
        sb.Append($"<div><strong>Order Reference:</strong> <span style='font-family:monospace;font-weight:700;'>{System.Net.WebUtility.HtmlEncode(order.PaymentReferenceCode ?? "N/A")}</span></div>");
        sb.Append($"<div style='margin-top:4px;'><strong>Total Order Amount:</strong> <strong>৳{order.Total:N0}</strong></div>");
        if (order.PaidAmount > 0)
        {
            sb.Append($"<div style='margin-top:4px;'><strong>bKash Paid:</strong> <strong style='color:#059669;'>৳{order.PaidAmount:N0}</strong> (TrxID: {System.Net.WebUtility.HtmlEncode(order.TransactionId ?? "Pending")})</div>");
        }
        else
        {
            sb.Append("<div style='margin-top:4px;color:#d97706;'><strong>Payment Status:</strong> Pending Verification</div>");
        }
        sb.Append("</div></div>");

        AppendOrderSummaryAndFooter(sb, order, baseUrl);

        return await SendHtmlAsync(recipientEmail, subject, sb.ToString());
    }

    public async Task<bool> SendPaymentVerifiedEmailAsync(Order order)
    {
        var recipientEmail = GetRecipientEmail(order);
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

        AppendOrderSummaryAndFooter(sb, order, baseUrl);

        return await SendHtmlAsync(recipientEmail, subject, sb.ToString());
    }

    public async Task<bool> SendOrderStatusUpdateEmailAsync(Order order, OrderStatus newStatus)
    {
        var recipientEmail = GetRecipientEmail(order);
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            log.LogInformation("No customer email available for Order #{OrderId}. Skipping status update email.", order.Id);
            return false;
        }

        var baseUrl = config["PublicBaseUrl"]?.TrimEnd('/') ?? "https://jasinternational.com";
        var customerName = order.DeliveryAddress?.CustomerName ?? "Valued Customer";

        string headerGradient;
        string badgeText;
        string subject;
        string mainHeading;
        string statusMessage;

        switch (newStatus)
        {
            case OrderStatus.Processing:
                headerGradient = "linear-gradient(135deg,#4338ca,#6366f1)";
                badgeText = "Order Confirmed & Preparing";
                subject = $"[JAS International] Order #{order.Id} Confirmed & In Progress";
                mainHeading = "Your Order is Confirmed!";
                statusMessage = $"Great news! Your <strong>Order #{order.Id}</strong> has been confirmed by our staff. Our fulfillment team is currently inspecting and packing your gadgets.";
                break;
            case OrderStatus.InTransit:
                headerGradient = "linear-gradient(135deg,#d97706,#f59e0b)";
                badgeText = "Shipped & In Transit";
                subject = $"[JAS International] Order #{order.Id} Has Been Dispatched!";
                mainHeading = "Your Package is On Its Way!";
                statusMessage = $"Your package for <strong>Order #{order.Id}</strong> has been handed over to our courier partner. It is now on its way to your destination.";
                break;
            case OrderStatus.Delivered:
                headerGradient = "linear-gradient(135deg,#059669,#10b981)";
                badgeText = "Delivered Successfully";
                subject = $"[JAS International] Order #{order.Id} Delivered - Thank You!";
                mainHeading = "Your Order Has Been Delivered!";
                statusMessage = $"Your package for <strong>Order #{order.Id}</strong> has been successfully delivered. We hope you enjoy your new tech! All official warranties are now activated.";
                break;
            case OrderStatus.Cancelled:
                headerGradient = "linear-gradient(135deg,#dc2626,#f43f5e)";
                badgeText = "Order Cancelled";
                subject = $"[JAS International] Notice: Order #{order.Id} Cancelled";
                mainHeading = "Order Cancelled";
                statusMessage = $"<strong>Order #{order.Id}</strong> has been cancelled. If you have any questions or require a refund of an advance payment, please reach out to our customer support.";
                break;
            default:
                return false;
        }

        var sb = new StringBuilder();
        sb.Append("<div style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:16px;overflow:hidden;'>");

        // Header
        sb.Append($"<div style='background:{headerGradient};padding:32px 24px;text-align:center;color:#ffffff;'>");
        sb.Append("<h1 style='margin:0;font-size:24px;font-weight:800;letter-spacing:-0.5px;'>JAS International</h1>");
        sb.Append($"<p style='margin:8px 0 0;font-size:13px;color:#ffffff;font-weight:600;text-transform:uppercase;letter-spacing:1px;opacity:0.9;'>{badgeText}</p>");
        sb.Append("</div>");

        // Body
        sb.Append("<div style='padding:28px 24px;color:#334155;font-size:14px;line-height:1.6;'>");
        sb.Append($"<p style='margin:0 0 16px;font-size:16px;font-weight:700;color:#0f172a;'>Dear {System.Net.WebUtility.HtmlEncode(customerName)},</p>");
        sb.Append($"<h2 style='margin:0 0 12px;font-size:18px;color:#0f172a;font-weight:700;'>{mainHeading}</h2>");
        sb.Append($"<p style='margin:0 0 20px;'>{statusMessage}</p>");

        AppendOrderSummaryAndFooter(sb, order, baseUrl);

        return await SendHtmlAsync(recipientEmail, subject, sb.ToString());
    }

    private void AppendOrderSummaryAndFooter(StringBuilder sb, Order order, string baseUrl)
    {
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
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(config["Mail:Host"], config.GetValue<int>("Mail:Port", 587))
        {
            EnableSsl = config.GetValue<bool>("Mail:EnableSsl", true)
        };
        var username = config["Mail:Username"];
        var password = config["Mail:Password"]?.Replace(" ", "");
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }
        return client;
    }

    public string Link(string path, HttpRequest request)
    {
        var origin = config["PublicBaseUrl"]?.TrimEnd('/') ?? $"{request.Scheme}://{request.Host}";
        return origin + path;
    }
}
