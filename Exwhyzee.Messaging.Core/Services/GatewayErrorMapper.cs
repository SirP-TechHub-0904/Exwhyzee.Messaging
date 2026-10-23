using System;
using System.Collections.Generic;
using System.Linq;

namespace Exwhyzee.Messaging.Core.Services
{
    /// <summary>
    /// Translates raw SMS gateway (KudiSMS) error codes into client-safe platform messages.
    /// Raw gateway text must never be shown to clients — it can reveal our own account
    /// state (e.g. gateway credit balance, tokens, routes).
    ///
    /// Platform code rule: "1009" + last two digits of the gateway code
    /// (exception: gateway 109 => 100990, because 100909 is used by gateway 009).
    /// </summary>
    public static class GatewayErrorMapper
    {
        public const string UnknownCode = "100999";

        private const string GenericMessage =
            "We could not send your message at this time. Please try again later or contact support.";

        public class GatewayError
        {
            public string GatewayCode { get; set; }
            public string GatewayMessage { get; set; }
            public string PlatformCode { get; set; }
            public string ClientMessage { get; set; }
            /// <summary>True when the problem is on our side (gateway account/config) and the client can't fix it.</summary>
            public bool IsInternal { get; set; }
            /// <summary>What an admin should do when this error is reported.</summary>
            public string AdminAction { get; set; }
        }

        public static readonly IReadOnlyList<GatewayError> All = new List<GatewayError>
        {
            // ---- Client-actionable errors ----
            new GatewayError { GatewayCode = "009", PlatformCode = "100909", IsInternal = false,
                GatewayMessage = "You are only allowed to send a maximum of 6 pages of SMS at once",
                ClientMessage  = "Your message is too long. A maximum of 6 pages can be sent at once.",
                AdminAction    = "Ask the client to shorten the message to 6 pages (918 GSM chars / 402 Unicode chars) or less." },
            new GatewayError { GatewayCode = "104", PlatformCode = "100904", IsInternal = false,
                GatewayMessage = "Blocked message keyword(s)",
                ClientMessage  = "Your message contains words that are not allowed. Please edit the content and try again.",
                AdminAction    = "Review the message content for restricted words. Consider adding the word to the platform blacklist so it is caught before sending." },
            new GatewayError { GatewayCode = "105", PlatformCode = "100905", IsInternal = false,
                GatewayMessage = "The sender ID used has been blocked",
                ClientMessage  = "This sender ID has been blocked. Please use a different sender ID.",
                AdminAction    = "Sender ID was blocked by the gateway/operator. Mark it as blocked in Sender IDs and advise the client to register a new one." },
            new GatewayError { GatewayCode = "106", PlatformCode = "100906", IsInternal = false,
                GatewayMessage = "The sender ID used do not exist",
                ClientMessage  = "The sender ID used does not exist or has not been approved.",
                AdminAction    = "Sender ID is not registered on the gateway. Submit it from Sender IDs and wait for approval." },
            new GatewayError { GatewayCode = "107", PlatformCode = "100907", IsInternal = false,
                GatewayMessage = "Please provide a valid phone number",
                ClientMessage  = "One or more phone numbers are invalid. Please check the recipients and try again.",
                AdminAction    = "Check the recipient list for malformed numbers (wrong length, letters, missing country code)." },
            new GatewayError { GatewayCode = "111", PlatformCode = "100911", IsInternal = false,
                GatewayMessage = "Only approved promotional Sender ID allowed",
                ClientMessage  = "Only an approved sender ID can be used for this message. Please select an approved sender ID.",
                AdminAction    = "Route requires an approved promotional sender ID. Confirm approval status on the gateway dashboard." },
            new GatewayError { GatewayCode = "188", PlatformCode = "100988", IsInternal = false,
                GatewayMessage = "The sender ID is unapproved",
                ClientMessage  = "This sender ID has not been approved yet. Please wait for approval or use an approved sender ID.",
                AdminAction    = "Sender ID is pending/rejected on the gateway. Follow up on approval in Sender IDs." },

            // ---- Internal / platform-side errors (hidden behind a generic message) ----
            new GatewayError { GatewayCode = "100", PlatformCode = "100900", IsInternal = true, GatewayMessage = "Token provided is invalid",
                AdminAction = "Gateway API token is wrong or expired. Update the token in API Settings." },
            new GatewayError { GatewayCode = "101", PlatformCode = "100901", IsInternal = true, GatewayMessage = "The account has been deactivated, please contact the admin",
                AdminAction = "Our KudiSMS account is deactivated. Contact KudiSMS support immediately." },
            new GatewayError { GatewayCode = "103", PlatformCode = "100903", IsInternal = true, GatewayMessage = "The gateway selected doesn't exist",
                AdminAction = "The gateway/route configured in API Settings is invalid. Correct the gateway value." },
            new GatewayError { GatewayCode = "108", PlatformCode = "100908", IsInternal = true, GatewayMessage = "The total amount of recipients is more than the required batch size of 100",
                AdminAction = "Platform sent more than 100 numbers in one gateway call. Check the batching logic in the SMS send service." },
            new GatewayError { GatewayCode = "109", PlatformCode = "100990", IsInternal = true, GatewayMessage = "You do not have enough credit balance to perform the transaction",
                AdminAction = "Our KudiSMS wallet is low/empty. Top up the gateway account balance, then ask the client to resend." },
            new GatewayError { GatewayCode = "114", PlatformCode = "100914", IsInternal = true, GatewayMessage = "No package attached to this service",
                AdminAction = "No pricing package is attached to our gateway account. Contact KudiSMS to assign a package." },
            new GatewayError { GatewayCode = "185", PlatformCode = "100985", IsInternal = true, GatewayMessage = "No route attached to this package",
                AdminAction = "Our gateway package has no delivery route. Contact KudiSMS to attach a route." },
            new GatewayError { GatewayCode = "187", PlatformCode = "100987", IsInternal = true, GatewayMessage = "The request could not be processed",
                AdminAction = "Temporary gateway failure. Retry later; if it persists, contact KudiSMS support." },
            new GatewayError { GatewayCode = "300", PlatformCode = "100930", IsInternal = true, GatewayMessage = "There are missing parameters",
                AdminAction = "Platform sent an incomplete request to the gateway. Check API Settings and the send request code." },
        };

        /// <summary>Finds the gateway error entry by code, falling back to matching the raw message text.</summary>
        public static GatewayError Find(string gatewayErrorCode, string gatewayMessage = null)
        {
            var code = gatewayErrorCode?.Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var byCode = All.FirstOrDefault(e => e.GatewayCode == code || e.GatewayCode.TrimStart('0') == code.TrimStart('0'));
                if (byCode != null) return byCode;
            }

            if (!string.IsNullOrWhiteSpace(gatewayMessage))
            {
                var msg = gatewayMessage.Trim();
                var byText = All.FirstOrDefault(e => msg.IndexOf(e.GatewayMessage, StringComparison.OrdinalIgnoreCase) >= 0);
                if (byText != null) return byText;

                if (msg.IndexOf("credit balance", StringComparison.OrdinalIgnoreCase) >= 0)
                    return All.First(e => e.GatewayCode == "109");
            }

            return null;
        }

        /// <summary>Returns the platform error code for a gateway response.</summary>
        public static string GetPlatformCode(string gatewayErrorCode, string gatewayMessage = null)
            => Find(gatewayErrorCode, gatewayMessage)?.PlatformCode ?? UnknownCode;

        /// <summary>Returns a client-safe message (with platform error code) for a gateway response.</summary>
        public static string ToClientMessage(string gatewayErrorCode, string gatewayMessage = null)
        {
            var err = Find(gatewayErrorCode, gatewayMessage);
            if (err == null)
                return $"{GenericMessage} (Error Code: {UnknownCode})";

            var text = err.IsInternal || string.IsNullOrEmpty(err.ClientMessage) ? GenericMessage : err.ClientMessage;
            return $"{text} (Error Code: {err.PlatformCode})";
        }
    }
}
