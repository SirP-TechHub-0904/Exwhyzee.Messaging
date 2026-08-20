using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MimeKit;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface IZeptoMailService : ISendEmail
    {
        Task<bool> SendContactInquiryAsync(string senderName, string senderEmail, string senderPhone, string inquiryMessage);
        Task<bool> SendEmailWithCustomRecipientAsync(string message, IEnumerable<string> recipients, string subject);
        Task<bool> SendEmailVerificationOtpAsync(string recipientEmail, string username, string otpCode);
        Task<bool> SendWelcomeEmailAsync(string recipientEmail, string username, decimal freeUnits);
        Task<bool> SendPasswordResetOtpAsync(string recipientEmail, string username, string otpCode);
        Task<bool> SendPaymentSuccessReceiptAsync(string recipientEmail, string username, decimal unitsPurchased, decimal totalNewBalance, decimal amountPaid, string transactionRef, string paymentMethod = "Online (Paystack)");
    }

    public class ZeptoMailService : IZeptoMailService
    {
        private readonly IConfiguration _configuration;

        public ZeptoMailService()
        {
            _configuration = BuildConfiguration();
        }

        public ZeptoMailService(IConfiguration configuration)
        {
            _configuration = configuration ?? BuildConfiguration();
        }

        private static IConfiguration BuildConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder();

                string jsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                }
                if (File.Exists(jsonPath))
                {
                    builder.AddJsonFile(jsonPath, optional: true, reloadOnChange: true);
                }

                // Check .env files
                var possibleEnvPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env")
                };

                foreach (var envPath in possibleEnvPaths)
                {
                    if (File.Exists(envPath))
                    {
                        foreach (var rawLine in File.ReadAllLines(envPath))
                        {
                            var line = rawLine.Trim();
                            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#") && line.Contains('='))
                            {
                                var parts = line.Split(new[] { '=' }, 2);
                                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                            }
                        }
                        break;
                    }
                }

                builder.AddEnvironmentVariables();
                return builder.Build();
            }
            catch { }
            return null;
        }

        private string LogoUrl => _configuration?["AppSettings:LogoUrl"] ?? "https://exwhyzee.ng/Content/image/SMS-LOGO.png";

        private string GetLogoFilePath()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "Content", "image", "SMS-LOGO.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Content", "image", "SMS-LOGO.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", "wwwroot", "Content", "image", "SMS-LOGO.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Exwhyzee.Messaging.Web", "wwwroot", "Content", "image", "SMS-LOGO.png")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }
            return null;
        }

        public async Task<bool> SendEmailAsync(string message, string recipient, string subject)
        {
            return await SendEmailWithCustomRecipientAsync(message, new[] { recipient }, subject);
        }

        public async Task<bool> SendEmailWithCustomRecipientAsync(string message, IEnumerable<string> recipients, string subject)
        {
            try
            {
                var host = _configuration?["ZeptoMailSettings:Host"] ?? "smtp.zeptomail.com";
                var portStr = _configuration?["ZeptoMailSettings:Port"] ?? "587";
                int.TryParse(portStr, out int port);
                if (port <= 0) port = 587;

                var username = _configuration?["ZeptoMailSettings:UserName"] ?? "emailapikey";
                var password = _configuration?["ZeptoMailSettings:Password"] ?? "";
                var fromEmail = _configuration?["ZeptoMailSettings:FromEmail"] ?? "noreply@xyzsms.com";
                var fromName = _configuration?["ZeptoMailSettings:FromName"] ?? "Exwhyzee Bulk SMS";

                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));

                foreach (var rec in recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    mimeMessage.To.Add(new MailboxAddress(rec.Trim(), rec.Trim()));
                }

                if (!mimeMessage.To.Any())
                {
                    return false;
                }

                mimeMessage.Subject = subject;

                // Build HTML body with inline embedded logo (CID) so it renders in all email clients without proxy errors
                var builder = new BodyBuilder();
                var logoPath = GetLogoFilePath();

                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                {
                    var imageResource = builder.LinkedResources.Add(logoPath);
                    imageResource.ContentId = "sms-logo";
                    message = message.Replace("{LogoUrl}", "cid:sms-logo");
                }
                else
                {
                    message = message.Replace("{LogoUrl}", LogoUrl);
                }

                builder.HtmlBody = message;
                mimeMessage.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                
                // Connect with STARTTLS on port 587
                await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    await client.AuthenticateAsync(username, password);
                }

                await client.SendAsync(mimeMessage);
                await client.DisconnectAsync(true);

                var recipientSummary = string.Join(", ", recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()));
                await LogEmailToDbAsync(recipientSummary, subject, message, isSuccess: true, errorMessage: null);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZeptoMail Exception]: {ex.Message}");
                var recipientSummary = string.Join(", ", recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()));
                await LogEmailToDbAsync(recipientSummary, subject, message, isSuccess: false, errorMessage: ex.Message);
                return false;
            }
        }

        private async Task LogEmailToDbAsync(string recipientEmail, string subject, string bodyHtml, bool isSuccess, string errorMessage)
        {
            try
            {
                using var db = new ApplicationDbContext();
                var log = new Exwhyzee.Messaging.Core.Models.EmailLog
                {
                    RecipientEmail = recipientEmail ?? "",
                    Subject = subject ?? "",
                    BodyHtml = bodyHtml ?? "",
                    DateSent = DateTime.UtcNow,
                    IsSuccess = isSuccess,
                    ErrorMessage = errorMessage
                };
                db.EmailLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailLog Exception]: {ex.Message}");
            }
        }

        public async Task<bool> SendEmailVerificationOtpAsync(string recipientEmail, string username, string otpCode)
        {
            var subject = $"{otpCode} is your Exwhyzee Bulk SMS Verification Code";
            var html = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 14px rgba(0,0,0,0.06);"">
    <div style=""background: #0B0F19; padding: 28px 32px; text-align: center; border-bottom: 3px solid #E50914;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 20px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 12px;"">
            <img src=""{{LogoUrl}}"" alt=""Exwhyzee Bulk SMS"" style=""height: 40px; width: auto; display: block; margin: 0 auto;"" />
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
            Email Address Verification
        </h1>
    </div>
    <div style=""padding: 36px 32px; color: #1e293b; line-height: 1.6;"">
        <h2 style=""font-size: 18px; font-weight: 700; color: #0f172a; margin-top: 0;"">Hello {username},</h2>
        <p style=""color: #475569; font-size: 15px;"">
            Thank you for registering on <strong>Exwhyzee Bulk SMS</strong>! Please use the 6-digit verification code below to verify your email address and activate your account:
        </p>

        <div style=""margin: 28px 0; text-align: center;"">
            <div style=""display: inline-block; background: #f8fafc; border: 2px dashed #E50914; border-radius: 8px; padding: 16px 36px; font-size: 32px; font-weight: 800; letter-spacing: 8px; font-family: monospace; color: #E50914;"">
                {otpCode}
            </div>
            <p style=""font-size: 12px; color: #94a3b8; margin: 10px 0 0;"">This code will expire in 15 minutes.</p>
        </div>

        <p style=""color: #64748b; font-size: 13px; margin-bottom: 0;"">
            Once verified, <strong>20 Free Test Messaging Units</strong> will be automatically credited to your wallet so you can start broadcasting immediately.
        </p>
    </div>
    <div style=""background: #f8fafc; padding: 16px 32px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9;"">
        &copy; {DateTime.UtcNow.Year} xyzsms
    </div>
</div>";

            return await SendEmailWithCustomRecipientAsync(html, new[] { recipientEmail }, subject);
        }

        public async Task<bool> SendWelcomeEmailAsync(string recipientEmail, string username, decimal freeUnits)
        {
            var subject = $"Welcome to Exwhyzee Bulk SMS! 20 Free Units Credited";
            var html = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 14px rgba(0,0,0,0.06);"">
    <div style=""background: #0B0F19; padding: 28px 32px; text-align: center; border-bottom: 3px solid #E50914;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 20px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 12px;"">
            <img src=""{{LogoUrl}}"" alt=""Exwhyzee Bulk SMS"" style=""height: 40px; width: auto; display: block; margin: 0 auto;"">
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
            Welcome to Exwhyzee Bulk SMS!
        </h1>
    </div>
    <div style=""padding: 36px 32px; color: #1e293b; line-height: 1.6;"">
        <h2 style=""font-size: 18px; font-weight: 700; color: #0f172a; margin-top: 0;"">Welcome aboard, {username}! 🎉</h2>
        <p style=""color: #475569; font-size: 15px;"">
            Your email has been verified and your account is now fully active. We have credited your wallet with <strong>{freeUnits:N0} Free Messaging Units</strong> to help you send your first broadcast!
        </p>

        <div style=""background: #f8fafc; border-radius: 8px; border: 1px solid #e2e8f0; padding: 20px; margin: 24px 0;"">
            <h3 style=""margin: 0 0 12px; font-size: 15px; color: #0f172a;"">What you can do next:</h3>
            <ul style=""margin: 0; padding-left: 20px; color: #475569; font-size: 14px; line-height: 1.8;"">
                <li>Create custom alphanumeric <strong>Sender IDs</strong> for your brand.</li>
                <li>Import your customer contact books seamlessly from Excel/CSV.</li>
                <li>Broadcast customer appreciation notices, price updates, and announcements.</li>
                <li>Top-up your wallet 24/7 with instant online payment.</li>
            </ul>
        </div>

        <div style=""text-align: center; margin-top: 28px;"">
            <a href=""https://exwhyzee.ng/Account/Login"" style=""background: #E50914; color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 6px; font-weight: 700; font-size: 15px; display: inline-block;"">
                Open Client Dashboard
            </a>
        </div>
    </div>
    <div style=""background: #f8fafc; padding: 16px 32px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9;"">
        &copy; {DateTime.UtcNow.Year} xyzsms
    </div>
</div>";

            return await SendEmailWithCustomRecipientAsync(html, new[] { recipientEmail }, subject);
        }

        public async Task<bool> SendPasswordResetOtpAsync(string recipientEmail, string username, string otpCode)
        {
            var subject = $"{otpCode} is your Exwhyzee Bulk SMS Password Reset Code";
            var html = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 14px rgba(0,0,0,0.06);"">
    <div style=""background: #0B0F19; padding: 28px 32px; text-align: center; border-bottom: 3px solid #E50914;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 20px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 12px;"">
            <img src=""{{LogoUrl}}"" alt=""Exwhyzee Bulk SMS"" style=""height: 40px; width: auto; display: block; margin: 0 auto;"" />
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
            Password Reset Request
        </h1>
    </div>
    <div style=""padding: 36px 32px; color: #1e293b; line-height: 1.6;"">
        <h2 style=""font-size: 18px; font-weight: 700; color: #0f172a; margin-top: 0;"">Hello {username},</h2>
        <p style=""color: #475569; font-size: 15px;"">
            We received a request to reset your <strong>Exwhyzee Bulk SMS</strong> account password. Enter the 6-digit code below to set a new password:
        </p>

        <div style=""margin: 28px 0; text-align: center;"">
            <div style=""display: inline-block; background: #f8fafc; border: 2px dashed #E50914; border-radius: 8px; padding: 16px 36px; font-size: 32px; font-weight: 800; letter-spacing: 8px; font-family: monospace; color: #E50914;"">
                {otpCode}
            </div>
            <p style=""font-size: 12px; color: #94a3b8; margin: 10px 0 0;"">This code will expire in 15 minutes.</p>
        </div>

        <p style=""color: #64748b; font-size: 13px; margin-bottom: 0;"">
            If you did not request a password reset, please ignore this email or contact support immediately.
        </p>
    </div>
    <div style=""background: #f8fafc; padding: 16px 32px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9;"">
        &copy; {DateTime.UtcNow.Year} xyzsms
    </div>
</div>";

            return await SendEmailWithCustomRecipientAsync(html, new[] { recipientEmail }, subject);
        }

        public async Task<bool> SendContactInquiryAsync(string senderName, string senderEmail, string senderPhone, string inquiryMessage)
        {
            var adminEmailsConfig = _configuration?["ZeptoMailSettings:ContactRecipientEmails"] 
                ?? _configuration?["AppSettings:ContactNotificationEmails"] 
                ?? string.Empty;

            var recipientList = adminEmailsConfig.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .ToList();

            var subject = $"[Contact Us Inquiry] New Message from {senderName}";

            var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 14px rgba(0,0,0,0.06);"">
    <div style=""background: #0B0F19; color: #ffffff; padding: 24px 28px; border-bottom: 3px solid #E50914; text-align: center;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 18px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 12px;"">
            <img src=""{{LogoUrl}}"" alt=""Exwhyzee Bulk SMS"" style=""height: 38px; width: auto; display: block; margin: 0 auto;"" />
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
            New Website Contact Inquiry
        </h1>
    </div>
    <div style=""padding: 24px 28px; color: #1e293b; line-height: 1.6;"">
        <p style=""margin-top: 0; font-size: 15px;"">You have received a new inquiry from the website contact form:</p>
        
        <table style=""width: 100%; border-collapse: collapse; margin: 16px 0; font-size: 14px;"">
            <tr style=""border-bottom: 1px solid #f1f5f9;"">
                <td style=""padding: 10px 0; font-weight: bold; width: 130px; color: #64748b;"">Full Name:</td>
                <td style=""padding: 10px 0; color: #0f172a; font-weight: 600;"">{senderName}</td>
            </tr>
            <tr style=""border-bottom: 1px solid #f1f5f9;"">
                <td style=""padding: 10px 0; font-weight: bold; color: #64748b;"">Email Address:</td>
                <td style=""padding: 10px 0; color: #0f172a;""><a href=""mailto:{senderEmail}"" style=""color: #E50914; text-decoration: none; font-weight: 600;"">{senderEmail}</a></td>
            </tr>
            <tr style=""border-bottom: 1px solid #f1f5f9;"">
                <td style=""padding: 10px 0; font-weight: bold; color: #64748b;"">Phone Number:</td>
                <td style=""padding: 10px 0; color: #0f172a;""><a href=""tel:{senderPhone}"" style=""color: #0f172a; text-decoration: none; font-weight: 600;"">{senderPhone}</a></td>
            </tr>
            <tr>
                <td style=""padding: 10px 0; font-weight: bold; color: #64748b; vertical-align: top;"">Date Received:</td>
                <td style=""padding: 10px 0; color: #0f172a;"">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td>
            </tr>
        </table>

        <div style=""margin-top: 20px; background: #f8fafc; border-left: 4px solid #E50914; padding: 16px; border-radius: 4px;"">
            <strong style=""display: block; margin-bottom: 8px; color: #0f172a;"">Message / Inquiry:</strong>
            <p style=""margin: 0; color: #334155; white-space: pre-wrap;"">{inquiryMessage}</p>
        </div>
    </div>
    <div style=""background: #f8fafc; padding: 16px 24px; font-size: 12px; color: #64748b; text-align: center; border-top: 1px solid #f1f5f9;"">
        &copy; {DateTime.UtcNow.Year} xyzsms
    </div>
</div>";

            return await SendEmailWithCustomRecipientAsync(htmlBody, recipientList, subject);
        }

        public async Task<bool> SendPaymentSuccessReceiptAsync(string recipientEmail, string username, decimal unitsPurchased, decimal totalNewBalance, decimal amountPaid, string transactionRef, string paymentMethod = "Online (Paystack)")
        {
            var subject = $"[XYZSMS] Unit Purchase Successful - {unitsPurchased:N0} Units Credited";
            var portalUrl = _configuration?["AppSettings:PortalUrl"] ?? "https://xyzsms.com";
            var buyUnitUrl = $"{portalUrl.TrimEnd('/')}/ClientPanel/Dashboard/BuyUnit";
            var dashboardUrl = $"{portalUrl.TrimEnd('/')}/ClientPanel/Dashboard";

            var html = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 14px; overflow: hidden; box-shadow: 0 4px 18px rgba(0,0,0,0.08);"">
    <div style=""background: #0B0F19; color: #ffffff; padding: 28px 32px; border-bottom: 4px solid #10B981; text-align: center;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 18px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 14px;"">
            <img src=""{{LogoUrl}}"" alt=""XYZSMS"" style=""height: 38px; width: auto; display: block; margin: 0 auto;"" />
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 22px; font-weight: 800; letter-spacing: -0.3px;"">
            Payment Confirmed &amp; Units Credited!
        </h1>
        <p style=""color: #10B981; margin: 6px 0 0; font-size: 14px; font-weight: 700;"">
            ✓ Transaction Successful
        </p>
    </div>
    <div style=""padding: 32px; color: #1e293b; line-height: 1.6;"">
        <p style=""margin-top: 0; font-size: 16px;"">
            Hello <strong>{username}</strong>,
        </p>
        <p style=""color: #475569; font-size: 15px;"">
            Your payment was processed successfully and your XYZSMS messaging wallet has been credited immediately.
        </p>

        <!-- RECEIPT SUMMARY BOX -->
        <div style=""background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 10px; padding: 20px 24px; margin: 24px 0;"">
            <table style=""width: 100%; border-collapse: collapse; font-size: 14px;"">
                <tr style=""border-bottom: 1px solid #E2E8F0;"">
                    <td style=""padding: 10px 0; color: #64748B; font-weight: 600;"">Units Added:</td>
                    <td style=""padding: 10px 0; color: #10B981; font-weight: 800; font-size: 18px; text-align: right;"">+{unitsPurchased:N0} Units</td>
                </tr>
                <tr style=""border-bottom: 1px solid #E2E8F0;"">
                    <td style=""padding: 10px 0; color: #64748B; font-weight: 600;"">Amount Paid:</td>
                    <td style=""padding: 10px 0; color: #0F172A; font-weight: 700; text-align: right;"">₦{amountPaid:N2}</td>
                </tr>
                <tr style=""border-bottom: 1px solid #E2E8F0;"">
                    <td style=""padding: 10px 0; color: #64748B; font-weight: 600;"">New Total Balance:</td>
                    <td style=""padding: 10px 0; color: #2563EB; font-weight: 800; font-size: 16px; text-align: right;"">{totalNewBalance:N2} Units</td>
                </tr>
                <tr style=""border-bottom: 1px solid #E2E8F0;"">
                    <td style=""padding: 10px 0; color: #64748B; font-weight: 600;"">Transaction Ref:</td>
                    <td style=""padding: 10px 0; color: #0F172A; font-family: monospace; font-weight: 600; text-align: right;"">{transactionRef}</td>
                </tr>
                <tr style=""border-bottom: 1px solid #E2E8F0;"">
                    <td style=""padding: 10px 0; color: #64748B; font-weight: 600;"">Payment Method:</td>
                    <td style=""padding: 10px 0; color: #0F172A; font-weight: 600; text-align: right;"">{paymentMethod}</td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0; color: #64748B; font-weight: 600;"">Date &amp; Time:</td>
                    <td style=""padding: 10px 0; color: #0F172A; font-weight: 600; text-align: right;"">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td>
                </tr>
            </table>
        </div>

        <!-- ACTION BUTTONS -->
        <div style=""margin: 32px 0 24px; text-align: center;"">
            <a href=""{buyUnitUrl}"" style=""display: inline-block; background: #E50914; color: #ffffff; text-decoration: none; font-weight: 700; font-size: 15px; padding: 14px 28px; border-radius: 8px; margin-right: 10px; margin-bottom: 8px; box-shadow: 0 4px 12px rgba(229, 9, 20, 0.3);"">
                💳 Add More Units
            </a>
            <a href=""{dashboardUrl}"" style=""display: inline-block; background: #0F172A; color: #ffffff; text-decoration: none; font-weight: 700; font-size: 15px; padding: 14px 28px; border-radius: 8px; margin-bottom: 8px;"">
                🚀 Launch Dashboard
            </a>
        </div>

        <p style=""color: #64748B; font-size: 13px; margin: 24px 0 0; text-align: center;"">
            Thank you for choosing XYZSMS for your high-speed messaging delivery. If you have questions regarding this payment, please contact our 24/7 support.
        </p>
    </div>
    <div style=""background: #F8FAFC; padding: 16px 32px; font-size: 12px; color: #94A3B8; text-align: center; border-top: 1px solid #F1F5F9;"">
        &copy; {DateTime.UtcNow.Year} XYZSMS &bull; Exwhyzee Smart Solutions Limited. All rights reserved.
    </div>
</div>";

            return await SendEmailWithCustomRecipientAsync(html, new[] { recipientEmail }, subject);
        }
    }
}
