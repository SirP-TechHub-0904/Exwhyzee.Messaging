using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface IGoogleReCaptchaService
    {
        Task<bool> VerifyTokenAsync(string recaptchaResponse);
        string SiteKey { get; }
    }

    public class GoogleReCaptchaService : IGoogleReCaptchaService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public GoogleReCaptchaService()
        {
            _httpClient = new HttpClient();
        }

        public GoogleReCaptchaService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public string SiteKey => _configuration?["GoogleReCaptcha:SiteKey"] 
            ?? Environment.GetEnvironmentVariable("GoogleReCaptcha__SiteKey") 
            ?? "6LciMY4tAAAAAAn4BSqUzeCAwQacub1GxgjeIwIG";

        private string SecretKey => _configuration?["GoogleReCaptcha:SecretKey"] 
            ?? Environment.GetEnvironmentVariable("GoogleReCaptcha__SecretKey") 
            ?? "6LciMY4tAAAAAKISGFpPZb-Ulj7cTwcXH6OitRpU";

        // Minimum score threshold for v3 (0.0 = bot, 1.0 = human). 0.5 is Google's recommended default.
        private double ScoreThreshold => 0.5;

        public async Task<bool> VerifyTokenAsync(string recaptchaResponse)
        {
            // Test key bypass for local development
            if (string.IsNullOrWhiteSpace(SecretKey) || SecretKey.StartsWith("YOUR_") || SecretKey == "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe")
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(recaptchaResponse))
            {
                return false;
            }

            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "secret", SecretKey },
                    { "response", recaptchaResponse }
                });

                var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);

                    bool success = false;
                    if (doc.RootElement.TryGetProperty("success", out var successProp))
                    {
                        success = successProp.GetBoolean();
                    }

                    if (!success) return false;

                    // For reCAPTCHA v3, check score
                    if (doc.RootElement.TryGetProperty("score", out var scoreProp))
                    {
                        double score = scoreProp.GetDouble();
                        return score >= ScoreThreshold;
                    }

                    // If no score property (v2 fallback), just return success
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[reCAPTCHA Verification Exception]: " + ex.Message);
            }

            return false;
        }
    }
}
