using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Services
{
    public class ErrorLogService : IErrorLogService
    {
        private readonly IConfiguration _configuration;
        private readonly IZeptoMailService _zeptoMailService;

        public ErrorLogService()
        {
            _configuration = BuildConfiguration();
            _zeptoMailService = new ZeptoMailService(_configuration);
        }

        public ErrorLogService(IConfiguration configuration, IZeptoMailService zeptoMailService)
        {
            _configuration = configuration ?? BuildConfiguration();
            _zeptoMailService = zeptoMailService ?? new ZeptoMailService(_configuration);
        }

        private static IConfiguration BuildConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder();

                string jsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(jsonPath))
                {
                    builder.AddJsonFile(jsonPath, optional: true, reloadOnChange: true);
                }

                // Check .env files
                var possibleEnvPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".env")
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
            catch
            {
                return null;
            }
        }

        private string GetSetting(string key, string defaultValue = "")
        {
            var val = _configuration?[key];
            if (!string.IsNullOrWhiteSpace(val)) return val;

            string envKey = key.Replace(":", "__");
            val = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(val)) return val;

            val = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(val)) return val;

            return defaultValue;
        }

        public async Task<string> LogErrorAndAlertAsync(
            Exception exception,
            string source,
            string logLevel = "Error",
            string userId = null,
            string userName = null,
            string userEmail = null,
            string requestUrl = null,
            string requestMethod = null,
            string clientIp = null,
            object payload = null)
        {
            if (exception == null) return "Unknown Error";

            // 1. Unroll exception tree & extract root cause
            var (fullMessage, innerDetails, rootCause) = ExtractExceptionDetails(exception);
            string stackTrace = exception.StackTrace ?? "";

            // Format payload data if provided
            string payloadString = null;
            if (payload != null)
            {
                try
                {
                    if (payload is string s) payloadString = s;
                    else payloadString = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    payloadString = payload.ToString();
                }
            }

            int? logRecordId = null;
            bool dbLogged = false;
            string dbLoggingError = null;

            // Generate guaranteed tracking Reference ID (e.g. ERR-20261014-9B3A1C)
            string incidentId = $"ERR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";

            // ========================================================
            // STAGE 1: ATTEMPT TO PERSIST TO DATABASE (SystemErrorLog)
            // ========================================================
            try
            {
                using var db = new ApplicationDbContext();
                var logEntry = new SystemErrorLog
                {
                    TimestampUtc = DateTime.UtcNow,
                    LogLevel = logLevel ?? "Error",
                    Source = $"[{incidentId}] {source ?? "System"}",
                    Message = fullMessage,
                    InnerErrorMessage = innerDetails,
                    StackTrace = stackTrace,
                    UserId = userId,
                    UserName = userName,
                    UserEmail = userEmail,
                    RequestUrl = requestUrl,
                    RequestMethod = requestMethod,
                    ClientIp = clientIp,
                    PayloadData = payloadString,
                    EmailAlertSent = false,
                    DbLoggingError = null
                };

                db.SystemErrorLogs.Add(logEntry);
                await db.SaveChangesAsync();

                dbLogged = true;
                logRecordId = logEntry.Id;
            }
            catch (Exception dbEx)
            {
                dbLogged = false;
                dbLoggingError = $"{dbEx.GetType().Name}: {dbEx.Message}";
                if (dbEx.InnerException != null)
                {
                    dbLoggingError += $" | Inner: {dbEx.InnerException.Message}";
                }
                Console.WriteLine($"[CRITICAL] ErrorLogService DB Insert Failed: {dbLoggingError}");
            }

            // ========================================================
            // STAGE 2: INDEPENDENT EMAIL ALERT (ALWAYS EXECUTES)
            // ========================================================
            bool emailSent = false;
            string emailSendError = null;

            try
            {
                // Destination: exwhyzeesms@gmail.com as configured in .env
                string rawRecipients = GetSetting("AppSettings:ErrorNotificationEmails", "exwhyzeesms@gmail.com");
                if (string.IsNullOrWhiteSpace(rawRecipients))
                {
                    rawRecipients = "exwhyzeesms@gmail.com";
                }

                var recipientList = rawRecipients
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Distinct()
                    .ToList();

                if (!recipientList.Any())
                {
                    recipientList.Add("exwhyzeesms@gmail.com");
                }

                string emailSubject = $"🚨 [{logLevel?.ToUpper() ?? "ERROR"}] [{incidentId}] {source}: {Truncate(rootCause, 50)}";
                string emailBodyHtml = BuildErrorAlertHtml(
                    incidentId: incidentId,
                    logLevel: logLevel,
                    source: source,
                    exception: exception,
                    fullMessage: fullMessage,
                    innerDetails: innerDetails,
                    rootCause: rootCause,
                    stackTrace: stackTrace,
                    dbLogged: dbLogged,
                    logRecordId: logRecordId,
                    dbLoggingError: dbLoggingError,
                    userId: userId,
                    userName: userName,
                    userEmail: userEmail,
                    requestUrl: requestUrl,
                    requestMethod: requestMethod,
                    clientIp: clientIp,
                    payloadString: payloadString
                );

                emailSent = await _zeptoMailService.SendEmailWithCustomRecipientAsync(emailBodyHtml, recipientList, emailSubject);
            }
            catch (Exception mailEx)
            {
                emailSent = false;
                emailSendError = $"{mailEx.GetType().Name}: {mailEx.Message}";
                Console.WriteLine($"[CRITICAL] ErrorLogService Email Dispatch Failed: {emailSendError}");
            }

            // If DB insert succeeded earlier, record whether email alert dispatched
            if (dbLogged && logRecordId.HasValue)
            {
                try
                {
                    using var updateDb = new ApplicationDbContext();
                    var existing = await updateDb.SystemErrorLogs.FirstOrDefaultAsync(x => x.Id == logRecordId.Value);
                    if (existing != null)
                    {
                        existing.EmailAlertSent = emailSent;
                        existing.EmailAlertError = emailSendError;
                        if (!string.IsNullOrEmpty(dbLoggingError)) existing.DbLoggingError = dbLoggingError;
                        await updateDb.SaveChangesAsync();
                    }
                }
                catch { }
            }

            return incidentId;
        }

        private static (string fullMessage, string innerDetails, string rootCause) ExtractExceptionDetails(Exception ex)
        {
            if (ex == null) return ("No Exception Details", "None", "Unknown");

            string fullMessage = $"{ex.GetType().Name}: {ex.Message}";
            var innerList = new List<string>();
            var current = ex.InnerException;
            string root = ex.Message;

            while (current != null)
            {
                innerList.Add($"{current.GetType().Name}: {current.Message}");
                root = current.Message;
                current = current.InnerException;
            }

            string innerDetails = innerList.Any() ? string.Join(" --> ", innerList) : "None";
            return (fullMessage, innerDetails, root);
        }

        private static string Truncate(string val, int maxLen)
        {
            if (string.IsNullOrEmpty(val)) return "";
            return val.Length <= maxLen ? val : val.Substring(0, maxLen) + "...";
        }

        private string BuildErrorAlertHtml(
            string incidentId,
            string logLevel,
            string source,
            Exception exception,
            string fullMessage,
            string innerDetails,
            string rootCause,
            string stackTrace,
            bool dbLogged,
            int? logRecordId,
            string dbLoggingError,
            string userId,
            string userName,
            string userEmail,
            string requestUrl,
            string requestMethod,
            string clientIp,
            string payloadString)
        {
            string dbBadge = dbLogged
                ? $"<span style=\"display:inline-block; padding:4px 10px; background:#D1FAE5; color:#065F46; border-radius:6px; font-weight:700; font-size:12px;\">✓ Saved to Database (ID: #{logRecordId})</span>"
                : $"<span style=\"display:inline-block; padding:4px 10px; background:#FEE2E2; color:#991B1B; border-radius:6px; font-weight:700; font-size:12px;\">⚠️ DB LOGGING FAILED ({System.Net.WebUtility.HtmlEncode(dbLoggingError ?? "Unknown")})</span>";

            string envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            return $@"
<div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 680px; margin: 0 auto; background: #ffffff; border: 1px solid #E2E8F0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.08);"">
    <!-- Header -->
    <div style=""background: #0F172A; padding: 24px 28px; border-bottom: 4px solid #EF4444;"">
        <div style=""display: flex; align-items: center; justify-content: space-between;"">
            <h2 style=""color: #EF4444; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
                🚨 CRITICAL SYSTEM EXCEPTION ALERT
            </h2>
        </div>
        <p style=""color: #94A3B8; margin: 6px 0 0 0; font-size: 13px;"">
            Exwhyzee Messaging System &bull; Environment: <b style=""color:#F8FAFC;"">{envName}</b> &bull; {timestamp}
        </p>
    </div>

    <!-- Main Content -->
    <div style=""padding: 24px 28px; color: #1E293B; line-height: 1.5;"">
        
        <!-- Incident Reference Badge -->
        <div style=""background: #FEF2F2; border: 1px solid #FCA5A5; border-radius: 8px; padding: 12px 16px; margin-bottom: 16px; display: flex; align-items: center; justify-content: space-between;"">
            <div>
                <span style=""font-size: 11px; text-transform: uppercase; font-weight: 800; color: #991B1B; display: block;"">Developer Incident Reference ID</span>
                <code style=""font-size: 16px; font-weight: 800; color: #B91C1C; font-family: Consolas, monospace;"">{incidentId}</code>
            </div>
            <span style=""font-size: 12px; color: #7F1D1D; font-weight: 600;"">User Facing Reference</span>
        </div>

        <!-- Status & Source Banner -->
        <div style=""display: flex; gap: 10px; margin-bottom: 16px; flex-wrap: wrap;"">
            {dbBadge}
            <span style=""display:inline-block; padding:4px 10px; background:#EFF6FF; color:#1E40AF; border-radius:6px; font-weight:600; font-size:12px;"">
                Source: {System.Net.WebUtility.HtmlEncode(source ?? "Application")}
            </span>
            <span style=""display:inline-block; padding:4px 10px; background:#FEF3C7; color:#92400E; border-radius:6px; font-weight:600; font-size:12px;"">
                Level: {System.Net.WebUtility.HtmlEncode(logLevel ?? "Error")}
            </span>
        </div>

        <!-- Root Cause Highlighting Box -->
        <div style=""background: #FEF2F2; border-left: 4px solid #DC2626; border-radius: 6px; padding: 14px 16px; margin-bottom: 20px;"">
            <div style=""font-size: 11px; text-transform: uppercase; font-weight: 800; color: #991B1B; margin-bottom: 4px; letter-spacing: 0.5px;"">
                Direct Root Cause / Inner Exception:
            </div>
            <div style=""font-family: Consolas, monospace; font-size: 14px; font-weight: 700; color: #7F1D1D; word-break: break-word;"">
                {System.Net.WebUtility.HtmlEncode(rootCause)}
            </div>
        </div>

        <!-- Diagnostics Table -->
        <table style=""width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 20px;"">
            <tr style=""border-bottom: 1px solid #F1F5F9;"">
                <td style=""padding: 8px 0; color: #64748B; font-weight: 600; width: 140px;"">Exception Type:</td>
                <td style=""padding: 8px 0; color: #0F172A; font-family: Consolas, monospace; font-weight: 600;"">{exception?.GetType().FullName}</td>
            </tr>
            <tr style=""border-bottom: 1px solid #F1F5F9;"">
                <td style=""padding: 8px 0; color: #64748B; font-weight: 600;"">Primary Message:</td>
                <td style=""padding: 8px 0; color: #0F172A; font-weight: 500;"">{System.Net.WebUtility.HtmlEncode(fullMessage)}</td>
            </tr>
            <tr style=""border-bottom: 1px solid #F1F5F9;"">
                <td style=""padding: 8px 0; color: #64748B; font-weight: 600;"">Inner Exception(s):</td>
                <td style=""padding: 8px 0; color: #475569; font-size: 12px;"">{System.Net.WebUtility.HtmlEncode(innerDetails)}</td>
            </tr>
            {(string.IsNullOrEmpty(requestUrl) ? "" : $@"
            <tr style=""border-bottom: 1px solid #F1F5F9;"">
                <td style=""padding: 8px 0; color: #64748B; font-weight: 600;"">Request:</td>
                <td style=""padding: 8px 0; color: #0F172A;""><b>{System.Net.WebUtility.HtmlEncode(requestMethod ?? "GET")}</b> {System.Net.WebUtility.HtmlEncode(requestUrl)} (IP: {System.Net.WebUtility.HtmlEncode(clientIp ?? "Unknown")})</td>
            </tr>")}
            {(string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(userId) ? "" : $@"
            <tr style=""border-bottom: 1px solid #F1F5F9;"">
                <td style=""padding: 8px 0; color: #64748B; font-weight: 600;"">Triggered By User:</td>
                <td style=""padding: 8px 0; color: #0F172A;""><b>{System.Net.WebUtility.HtmlEncode(userName ?? "N/A")}</b> &bull; User ID: {System.Net.WebUtility.HtmlEncode(userId ?? "N/A")} &bull; Email: {System.Net.WebUtility.HtmlEncode(userEmail ?? "N/A")}</td>
            </tr>")}
            {(!dbLogged ? $@"
            <tr style=""border-bottom: 1px solid #F1F5F9; background: #FFF5F5;"">
                <td style=""padding: 8px 0; color: #DC2626; font-weight: 700;"">Database Error:</td>
                <td style=""padding: 8px 0; color: #991B1B; font-family: Consolas, monospace; font-size: 12px;"">{System.Net.WebUtility.HtmlEncode(dbLoggingError ?? "N/A")}</td>
            </tr>" : "")}
        </table>

        <!-- Contextual Payload / Metadata -->
        {(string.IsNullOrEmpty(payloadString) ? "" : $@"
        <div style=""margin-bottom: 20px;"">
            <div style=""font-size: 12px; font-weight: 700; color: #334155; margin-bottom: 6px;"">
                Contextual Payload / Operation Parameters:
            </div>
            <pre style=""background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 6px; padding: 12px; font-size: 12px; color: #334155; overflow-x: auto; white-space: pre-wrap; font-family: Consolas, monospace; margin: 0;"">{System.Net.WebUtility.HtmlEncode(payloadString)}</pre>
        </div>")}

        <!-- Stack Trace Block -->
        <div>
            <div style=""font-size: 12px; font-weight: 700; color: #334155; margin-bottom: 6px;"">
                Stack Trace:
            </div>
            <pre style=""background: #0F172A; color: #E2E8F0; border-radius: 6px; padding: 14px; font-size: 11px; overflow-x: auto; white-space: pre-wrap; font-family: Consolas, monospace; margin: 0; line-height: 1.5; max-height: 320px;"">{System.Net.WebUtility.HtmlEncode(stackTrace)}</pre>
        </div>
    </div>

    <!-- Footer -->
    <div style=""background: #F8FAFC; padding: 14px 28px; border-top: 1px solid #E2E8F0; text-align: center; color: #94A3B8; font-size: 12px;"">
        This automated emergency alert was generated by Exwhyzee Messaging Error Surveillance.
    </div>
</div>
";
        }
    }
}
