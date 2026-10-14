using System;
using System.IO;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Services;

namespace Exwhyzee.Messaging.Core
{
    public sealed class ExceptionUtility
    {
        private static readonly IErrorLogService _errorLogService = new ErrorLogService();

        public ExceptionUtility()
        { }

        // Synchronous logging (backward compatibility) - fires async DB log & email alert
        public static void LogException(Exception exc, string source)
        {
            if (exc == null) return;

            // 1. Try file log as local safety net
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string logFile = Path.Combine(dir, "ErrorLog.txt");
                using var sw = new StreamWriter(logFile, true);
                sw.WriteLine("********** {0} **********", DateTime.UtcNow);
                sw.WriteLine("Source: " + source);
                sw.WriteLine("Exception Type: " + exc.GetType().FullName);
                sw.WriteLine("Exception: " + exc.Message);
                if (exc.InnerException != null)
                {
                    sw.WriteLine("Inner: " + exc.InnerException.Message);
                }
                sw.WriteLine("Stack: " + exc.StackTrace);
                sw.WriteLine();
            }
            catch { }

            // 2. Fire and forget DB logging & email alert
            _ = Task.Run(async () =>
            {
                try
                {
                    await _errorLogService.LogErrorAndAlertAsync(exc, source);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ExceptionUtility.LogException Alert Failure]: {ex.Message}");
                }
            });
        }

        // Asynchronous logging & alerting with diagnostic root-cause extraction
        public static async Task<string> LogAndAlertAsync(
            Exception exc, 
            string source, 
            string userId = null, 
            string userName = null, 
            string userEmail = null, 
            string requestUrl = null, 
            string requestMethod = null, 
            string clientIp = null, 
            object payload = null)
        {
            if (exc == null) return "Unknown Error";
            return await _errorLogService.LogErrorAndAlertAsync(
                exc, 
                source, 
                logLevel: "Critical",
                userId: userId,
                userName: userName,
                userEmail: userEmail,
                requestUrl: requestUrl,
                requestMethod: requestMethod,
                clientIp: clientIp,
                payload: payload);
        }

        // Notify System Operators about an exception 
        public static void NotifySystemOps(Exception exc)
        {
            LogException(exc, "SystemOpsNotification");
        }
    }
}
