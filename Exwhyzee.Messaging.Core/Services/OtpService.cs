using System;
using System.Collections.Concurrent;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface IOtpService
    {
        string GenerateOtp(string email, string purpose);
        bool ValidateOtp(string email, string purpose, string code);
        void RemoveOtp(string email, string purpose);
    }

    public class OtpRecord
    {
        public string Code { get; set; }
        public DateTime ExpiryTime { get; set; }
    }

    public class OtpService : IOtpService
    {
        private static readonly ConcurrentDictionary<string, OtpRecord> _otpStore = new ConcurrentDictionary<string, OtpRecord>();

        private string GetKey(string email, string purpose) => $"{email.Trim().ToLowerInvariant()}_{purpose.Trim().ToLowerInvariant()}";

        public string GenerateOtp(string email, string purpose)
        {
            var key = GetKey(email, purpose);
            var code = Random.Shared.Next(100000, 999999).ToString();
            
            var record = new OtpRecord
            {
                Code = code,
                ExpiryTime = DateTime.UtcNow.AddMinutes(15)
            };

            _otpStore[key] = record;
            return code;
        }

        public bool ValidateOtp(string email, string purpose, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)) return false;

            var key = GetKey(email, purpose);
            if (_otpStore.TryGetValue(key, out var record))
            {
                if (DateTime.UtcNow <= record.ExpiryTime && record.Code == code.Trim())
                {
                    _otpStore.TryRemove(key, out _);
                    return true;
                }
            }

            return false;
        }

        public void RemoveOtp(string email, string purpose)
        {
            var key = GetKey(email, purpose);
            _otpStore.TryRemove(key, out _);
        }
    }
}
