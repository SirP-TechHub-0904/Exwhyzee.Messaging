using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface IPaystackTransferService
    {
        Task<(bool Success, string AccountName, string Message)> ResolveBankAccountAsync(string accountNumber, string bankCode);
        Task<(bool Success, string RecipientCode, string Message)> CreateTransferRecipientAsync(string accountName, string accountNumber, string bankCode);
        Task<(bool Success, string TransferCode, string Message)> InitiateTransferAsync(decimal amount, string recipientCode, string reason);
        Task<(bool Success, string Status, string Message, decimal? Amount, string FailReason, string RecipientName, string BankName, string AccountNumber)> FetchTransferDetailsAsync(string transferCode);
    }

    public class PaystackTransferService : IPaystackTransferService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public PaystackTransferService()
        {
            _configuration = BuildConfiguration();
            _httpClient = new HttpClient();
        }

        public PaystackTransferService(IConfiguration configuration)
        {
            _configuration = configuration ?? BuildConfiguration();
            _httpClient = new HttpClient();
        }

        private static IConfiguration BuildConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder();

                string jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!System.IO.File.Exists(jsonPath))
                {
                    jsonPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "appsettings.json");
                }
                if (System.IO.File.Exists(jsonPath))
                {
                    builder.AddJsonFile(jsonPath, optional: true, reloadOnChange: true);
                }

                // Check .env files
                var possibleEnvPaths = new[]
                {
                    System.IO.Path.Combine(AppContext.BaseDirectory, ".env"),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".env"),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env")
                };

                foreach (var envPath in possibleEnvPaths)
                {
                    if (System.IO.File.Exists(envPath))
                    {
                        foreach (var rawLine in System.IO.File.ReadAllLines(envPath))
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

        private string SecretKey
        {
            get
            {
                var key = _configuration?["PaystackSettings:SecretKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Environment.GetEnvironmentVariable("PaystackSettings__SecretKey")
                    ?? Environment.GetEnvironmentVariable("PayStackSecretKey");
                if (!string.IsNullOrWhiteSpace(key)) return key;

                return "sk_test_8f91d590f63da0677e1bbd5f47f9f0a8b2bc5119";
            }
        }

        private void SetAuthHeader()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<(bool Success, string AccountName, string Message)> ResolveBankAccountAsync(string accountNumber, string bankCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(bankCode))
                {
                    return (false, null, "Account number and bank code are required.");
                }

                SetAuthHeader();
                string url = $"https://api.paystack.co/bank/resolve?account_number={accountNumber.Trim()}&bank_code={bankCode.Trim()}";

                var response = await _httpClient.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool status = root.TryGetProperty("status", out var statusProp) && statusProp.GetBoolean();
                string message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unable to resolve account.";

                if (status && root.TryGetProperty("data", out var dataProp))
                {
                    string accountName = dataProp.TryGetProperty("account_name", out var nameProp) ? nameProp.GetString() : null;
                    if (!string.IsNullOrEmpty(accountName))
                    {
                        return (true, accountName, "Account resolved successfully.");
                    }
                }

                return (false, null, message);
            }
            catch (Exception ex)
            {
                return (false, null, $"Paystack bank resolution failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string RecipientCode, string Message)> CreateTransferRecipientAsync(string accountName, string accountNumber, string bankCode)
        {
            try
            {
                SetAuthHeader();
                string url = "https://api.paystack.co/transferrecipient";

                var payload = new
                {
                    type = "nuban",
                    name = accountName,
                    account_number = accountNumber,
                    bank_code = bankCode,
                    currency = "NGN"
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool status = root.TryGetProperty("status", out var statusProp) && statusProp.GetBoolean();
                string message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Failed to create transfer recipient.";

                if (status && root.TryGetProperty("data", out var dataProp))
                {
                    string recipientCode = dataProp.TryGetProperty("recipient_code", out var codeProp) ? codeProp.GetString() : null;
                    if (!string.IsNullOrEmpty(recipientCode))
                    {
                        return (true, recipientCode, "Transfer recipient created successfully.");
                    }
                }

                return (false, null, message);
            }
            catch (Exception ex)
            {
                return (false, null, $"Paystack recipient creation failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string TransferCode, string Message)> InitiateTransferAsync(decimal amount, string recipientCode, string reason)
        {
            try
            {
                if (amount <= 0)
                {
                    return (false, null, "Transfer amount must be greater than zero.");
                }
                if (string.IsNullOrWhiteSpace(recipientCode))
                {
                    return (false, null, "Valid Paystack recipient code is required.");
                }

                SetAuthHeader();
                string url = "https://api.paystack.co/transfer";

                int amountInKobo = (int)Math.Round(amount * 100);

                var payload = new
                {
                    source = "balance",
                    amount = amountInKobo,
                    recipient = recipientCode,
                    reason = string.IsNullOrWhiteSpace(reason) ? "Gateway Outflow Settlement" : reason
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool status = root.TryGetProperty("status", out var statusProp) && statusProp.GetBoolean();
                string message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Paystack transfer request failed.";

                if (status && root.TryGetProperty("data", out var dataProp))
                {
                    string transferCode = dataProp.TryGetProperty("transfer_code", out var codeProp) ? codeProp.GetString() : null;
                    return (true, transferCode, message ?? "Transfer initiated successfully.");
                }

                return (false, null, message);
            }
            catch (Exception ex)
            {
                return (false, null, $"Paystack transfer execution failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Status, string Message, decimal? Amount, string FailReason, string RecipientName, string BankName, string AccountNumber)> FetchTransferDetailsAsync(string transferCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transferCode))
                {
                    return (false, null, "Transfer code is required.", null, null, null, null, null);
                }

                SetAuthHeader();
                string cleanCode = transferCode.Trim();
                string url = $"https://api.paystack.co/transfer/{cleanCode}";

                var response = await _httpClient.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool status = root.TryGetProperty("status", out var statusProp) && statusProp.GetBoolean();
                string message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unable to query transfer.";

                if (status && root.TryGetProperty("data", out var dataProp))
                {
                    string transferStatus = dataProp.TryGetProperty("status", out var sProp) ? sProp.GetString() : "unknown";
                    
                    decimal? amount = null;
                    if (dataProp.TryGetProperty("amount", out var amtProp) && amtProp.TryGetInt64(out var amtKobo))
                    {
                        amount = amtKobo / 100m;
                    }

                    string failReason = null;
                    if (dataProp.TryGetProperty("failures", out var failuresProp) && failuresProp.ValueKind == JsonValueKind.Array && failuresProp.GetArrayLength() > 0)
                    {
                        failReason = failuresProp[0].ToString();
                    }
                    else if (dataProp.TryGetProperty("reason", out var reasonProp))
                    {
                        failReason = reasonProp.GetString();
                    }

                    string recipientName = null;
                    string bankName = null;
                    string accountNumber = null;

                    if (dataProp.TryGetProperty("recipient", out var recProp) && recProp.ValueKind == JsonValueKind.Object)
                    {
                        if (recProp.TryGetProperty("name", out var rNameProp))
                        {
                            recipientName = rNameProp.GetString();
                        }

                        if (recProp.TryGetProperty("details", out var detProp) && detProp.ValueKind == JsonValueKind.Object)
                        {
                            if (detProp.TryGetProperty("account_name", out var accNameProp) && !string.IsNullOrWhiteSpace(accNameProp.GetString()))
                            {
                                recipientName = accNameProp.GetString();
                            }
                            if (detProp.TryGetProperty("account_number", out var accNumProp))
                            {
                                accountNumber = accNumProp.GetString();
                            }
                            if (detProp.TryGetProperty("bank_name", out var bNameProp))
                            {
                                bankName = bNameProp.GetString();
                            }
                        }
                    }

                    return (true, transferStatus, message, amount, failReason, recipientName, bankName, accountNumber);
                }

                return (false, null, message, null, null, null, null, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Failed to query Paystack transfer: {ex.Message}", null, null, null, null, null);
            }
        }
    }
}
