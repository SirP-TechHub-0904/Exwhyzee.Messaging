using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Data.Services;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface ITwoFactorService
    {
        string GenerateSecretKey();
        string GenerateQrCodeUri(string username, string secretKey);
        string GenerateQrCodeImageUrl(string qrUri);
        bool ValidateTotpCode(string secretKey, string code);
        string Generate6DigitOtp();
        Task<bool> SendSmsOtpAsync(string phoneNumber, string otpCode);
        Task<bool> SendEmailOtpAsync(string email, string username, string otpCode);
    }

    public class TwoFactorService : ITwoFactorService
    {
        private static readonly char[] Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

        public string GenerateSecretKey()
        {
            byte[] bytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var result = new StringBuilder(32);
            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(Base32Chars[bytes[i] % 32]);
            }
            return result.ToString();
        }

        public string GenerateQrCodeUri(string username, string secretKey)
        {
            string cleanUsername = Uri.EscapeDataString(username ?? "User");
            return $"otpauth://totp/XYZSMS:{cleanUsername}?secret={secretKey}&issuer=XYZSMS";
        }

        public string GenerateQrCodeImageUrl(string qrUri)
        {
            string encoded = Uri.EscapeDataString(qrUri);
            return $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={encoded}";
        }

        public string Generate6DigitOtp()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int val = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
                return val.ToString("D6");
            }
        }

        public bool ValidateTotpCode(string secretKey, string code)
        {
            if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();

            try
            {
                byte[] key = Base32Decode(secretKey);
                long currentStep = DateTime.UtcNow.Ticks / (30L * 10000000L); // 30-second time step

                // Allow a window of +-1 time step (tolerance for clock skew)
                for (long step = currentStep - 1; step <= currentStep + 1; step++)
                {
                    string computedCode = ComputeHmacSha1(key, step);
                    if (string.Equals(computedCode, code, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        public async Task<bool> SendSmsOtpAsync(string phoneNumber, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return false;
            try
            {
                var clientService = new ClientService();
                string message = $"Your XYZSMS 2FA Verification Code is: {otpCode}. Valid for 10 minutes. Do not share this code.";
                var response = await clientService.SendSms("XYZSMS", message, phoneNumber);
                return response != null && (response.status == "success" || (response.msg != null && response.msg.ToLower().Contains("success")));
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendEmailOtpAsync(string email, string username, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var mailService = new ZeptoMailService();
                return await mailService.SendEmailVerificationOtpAsync(email, username, otpCode);
            }
            catch
            {
                return false;
            }
        }

        private byte[] Base32Decode(string input)
        {
            input = input.TrimEnd('=').ToUpperInvariant();
            if (string.IsNullOrEmpty(input)) return new byte[0];

            int byteCount = input.Length * 5 / 8;
            byte[] returnBytes = new byte[byteCount];

            byte curByte = 0, bitsRemaining = 8;
            int arrayIndex = 0;

            foreach (char c in input)
            {
                int cValue = Base32CharsString.IndexOf(c);
                if (cValue < 0) continue;

                if (bitsRemaining > 5)
                {
                    mask = cValue << (bitsRemaining - 5);
                    curByte |= (byte)mask;
                    bitsRemaining -= 5;
                }
                else
                {
                    int mask = cValue >> (5 - bitsRemaining);
                    curByte |= (byte)mask;
                    returnBytes[arrayIndex++] = curByte;
                    curByte = (byte)((cValue << (3 + bitsRemaining)) & 255);
                    bitsRemaining += 3;
                }
            }

            return returnBytes;
        }

        private const string Base32CharsString = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private int mask = 0;

        private string ComputeHmacSha1(byte[] key, long step)
        {
            byte[] stepBytes = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(stepBytes);
            }

            using (var hmac = new HMACSHA1(key))
            {
                byte[] hash = hmac.ComputeHash(stepBytes);
                int offset = hash[hash.Length - 1] & 0x0F;
                int binary =
                    ((hash[offset] & 0x7F) << 24) |
                    ((hash[offset + 1] & 0xFF) << 16) |
                    ((hash[offset + 2] & 0xFF) << 8) |
                    (hash[offset + 3] & 0xFF);

                int otp = binary % 1000000;
                return otp.ToString("D6");
            }
        }
    }
}
