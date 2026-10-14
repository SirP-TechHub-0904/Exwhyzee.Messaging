using System;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface IErrorLogService
    {
        /// <summary>
        /// Logs an error to the SystemErrorLog table and dispatches an emergency email alert.
        /// Even if database logging fails, email notification is guaranteed to execute.
        /// </summary>
        /// <returns>The extracted root cause / inner exception message for user-friendly diagnostics.</returns>
        Task<string> LogErrorAndAlertAsync(
            Exception exception,
            string source,
            string logLevel = "Error",
            string userId = null,
            string userName = null,
            string userEmail = null,
            string requestUrl = null,
            string requestMethod = null,
            string clientIp = null,
            object payload = null);
    }
}
