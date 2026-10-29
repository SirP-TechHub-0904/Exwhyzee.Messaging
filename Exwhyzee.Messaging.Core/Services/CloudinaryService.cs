using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Exwhyzee.Messaging.Core.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private static long _clockOffsetSeconds = 0;
        public string LastError { get; private set; }

        public string CloudName { get; private set; }
        public string ApiKey { get; private set; }
        public string ApiSecret { get; private set; }
        public string RootFolder { get; private set; } = "xyzsms";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(CloudName) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(ApiSecret) &&
            !ApiSecret.Contains("<your_api_secret>", StringComparison.OrdinalIgnoreCase) &&
            !ApiSecret.Contains("YOUR_API_SECRET", StringComparison.OrdinalIgnoreCase);

        public CloudinaryService(IConfiguration config = null, IWebHostEnvironment env = null)
        {
            _config = config;
            _env = env;
            LoadCredentials();
        }

        private void LoadCredentials()
        {
            string cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL")
                ?? _config?["CLOUDINARY_URL"]
                ?? _config?["CloudinarySettings:CloudinaryUrl"];

            if (!string.IsNullOrWhiteSpace(cloudinaryUrl) && cloudinaryUrl.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Format: cloudinary://api_key:api_secret@cloud_name
                    string withoutScheme = cloudinaryUrl.Substring("cloudinary://".Length).Trim();
                    int atIndex = withoutScheme.LastIndexOf('@');
                    if (atIndex > 0)
                    {
                        string creds = withoutScheme.Substring(0, atIndex);
                        CloudName = withoutScheme.Substring(atIndex + 1).Trim().TrimEnd('/');
                        int colonIndex = creds.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            ApiKey = creds.Substring(0, colonIndex).Trim().Trim('<', '>');
                            ApiSecret = creds.Substring(colonIndex + 1).Trim().Trim('<', '>');
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(CloudName))
            {
                CloudName = Environment.GetEnvironmentVariable("CloudinarySettings__CloudName")
                    ?? _config?["CloudinarySettings:CloudName"]
                    ?? "yffz4ii9";
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                ApiKey = Environment.GetEnvironmentVariable("CloudinarySettings__ApiKey")
                    ?? _config?["CloudinarySettings:ApiKey"]
                    ?? "443845997859478";
            }

            if (string.IsNullOrWhiteSpace(ApiSecret) || ApiSecret.Contains("your_api_secret", StringComparison.OrdinalIgnoreCase))
            {
                string envSecret = Environment.GetEnvironmentVariable("CloudinarySettings__ApiSecret")
                    ?? _config?["CloudinarySettings:ApiSecret"];
                if (!string.IsNullOrWhiteSpace(envSecret))
                {
                    ApiSecret = envSecret.Trim().Trim('<', '>');
                }
            }

            string folderPrefix = Environment.GetEnvironmentVariable("CloudinarySettings__FolderPrefix")
                ?? _config?["CloudinarySettings:FolderPrefix"];
            if (!string.IsNullOrWhiteSpace(folderPrefix))
            {
                RootFolder = folderPrefix.Trim().Trim('/');
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string subFolder = "general", string publicIdPrefix = "file")
        {
            if (file == null || file.Length <= 0) return null;
            using var stream = file.OpenReadStream();
            return await UploadStreamAsync(stream, file.FileName, subFolder, publicIdPrefix);
        }

        public async Task<string> UploadStreamAsync(Stream stream, string fileName, string subFolder = "general", string publicIdPrefix = "file")
        {
            if (stream == null) return null;

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] fileBytes = ms.ToArray();
            if (fileBytes.Length == 0) return null;

            string ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? ".jpg";
            string cleanPrefix = SanitizeSegment(publicIdPrefix);
            string cleanSubFolder = SanitizeSegment(subFolder);
            string folderPath = string.IsNullOrWhiteSpace(cleanSubFolder) ? RootFolder : $"{RootFolder}/{cleanSubFolder}";
            string publicId = $"{cleanPrefix}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            // Upload to Cloudinary if API Secret is configured
            if (IsConfigured)
            {
                try
                {
                    string endpoint = $"https://api.cloudinary.com/v1_1/{CloudName}/auto/upload";

                    async Task<(HttpResponseMessage Resp, string Body)> AttemptCloudinaryUploadAsync(long ts)
                    {
                        var signParams = new SortedDictionary<string, string>
                        {
                            { "folder", folderPath },
                            { "public_id", publicId },
                            { "timestamp", ts.ToString() }
                        };

                        string signature = GenerateSignature(signParams, ApiSecret);

                        using var form = new MultipartFormDataContent();
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(ext));
                        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? $"{publicId}{ext}" : Path.GetFileName(fileName));
                        form.Add(new StringContent(ApiKey), "api_key");
                        form.Add(new StringContent(ts.ToString()), "timestamp");
                        form.Add(new StringContent(folderPath), "folder");
                        form.Add(new StringContent(publicId), "public_id");
                        form.Add(new StringContent(signature), "signature");

                        var resp = await _httpClient.PostAsync(endpoint, form);
                        string body = await resp.Content.ReadAsStringAsync();
                        return (resp, body);
                    }

                    long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _clockOffsetSeconds;
                    var (response, json) = await AttemptCloudinaryUploadAsync(timestamp);

                    // If local system clock is skewed (e.g. year 2026 vs server UTC), sync from Cloudinary's HTTP Date header and retry immediately
                    if (!response.IsSuccessStatusCode && response.Headers.Date.HasValue)
                    {
                        long serverTs = response.Headers.Date.Value.ToUnixTimeSeconds();
                        _clockOffsetSeconds = serverTs - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        (response, json) = await AttemptCloudinaryUploadAsync(serverTs);
                    }

                    if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("secure_url", out var secureUrlProp))
                        {
                            string secureUrl = secureUrlProp.GetString();
                            if (!string.IsNullOrWhiteSpace(secureUrl))
                            {
                                return secureUrl;
                            }
                        }
                        if (doc.RootElement.TryGetProperty("url", out var urlProp))
                        {
                            string url = urlProp.GetString();
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                return url.Replace("http://", "https://");
                            }
                        }
                    }
                    else
                    {
                        LastError = $"HTTP {(int)response.StatusCode}: {json}";
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }
            }

            // Resilient fallback to wwwroot/Uploads/{subFolder} if Cloudinary secret is not yet filled in .env
            string webRoot = _env?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string localDir = Path.Combine(webRoot, "Uploads", string.IsNullOrWhiteSpace(cleanSubFolder) ? "general" : cleanSubFolder);
            if (!Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            string localFileName = $"{publicId}{ext}";
            string fullLocalPath = Path.Combine(localDir, localFileName);
            await File.WriteAllBytesAsync(fullLocalPath, fileBytes);
            return $"/Uploads/{(string.IsNullOrWhiteSpace(cleanSubFolder) ? "general" : cleanSubFolder)}/{localFileName}";
        }

        private static string GenerateSignature(SortedDictionary<string, string> parameters, string apiSecret)
        {
            string toSign = string.Join("&", parameters.Where(kv => !string.IsNullOrEmpty(kv.Value)).Select(kv => $"{kv.Key}={kv.Value}")) + apiSecret;
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string SanitizeSegment(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "file";
            var chars = input.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray();
            return chars.Length > 0 ? new string(chars) : "file";
        }

        private static string GetMimeType(string ext)
        {
            return (ext ?? "").ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                _ => "image/jpeg"
            };
        }
    }
}

