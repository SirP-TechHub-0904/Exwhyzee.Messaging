using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Exwhyzee.Messaging.Web
{
    public class AppConfig
    {
        public static IConfiguration Configuration { get; set; }

        public static string PayStackSecretKey
        {
            get
            {
                var key = Configuration?["PaystackSettings:SecretKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Configuration?["AppSettings:PayStackSecretKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Configuration?["PayStackSecretKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Environment.GetEnvironmentVariable("PaystackSettings__SecretKey") 
                    ?? Environment.GetEnvironmentVariable("PayStackSecretKey");
                if (!string.IsNullOrWhiteSpace(key)) return key;

                // Fallback: check .env directly if running outside standard host pipeline
                key = LoadFromEnvFile("PaystackSettings__SecretKey") ?? LoadFromEnvFile("PayStackSecretKey");
                if (!string.IsNullOrWhiteSpace(key)) return key;

                return string.Empty;
            }
        }

        public static string PayStackPublicKey
        {
            get
            {
                var key = Configuration?["PaystackSettings:PublicKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Configuration?["AppSettings:PayStackPublicKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Configuration?["PayStackPublicKey"];
                if (!string.IsNullOrWhiteSpace(key)) return key;

                key = Environment.GetEnvironmentVariable("PaystackSettings__PublicKey")
                    ?? Environment.GetEnvironmentVariable("PayStackPublicKey");
                if (!string.IsNullOrWhiteSpace(key)) return key;

                // Fallback: check .env directly
                key = LoadFromEnvFile("PaystackSettings__PublicKey") ?? LoadFromEnvFile("PayStackPublicKey");
                if (!string.IsNullOrWhiteSpace(key)) return key;

                return string.Empty;
            }
        }

        private static string LoadFromEnvFile(string keyName)
        {
            try
            {
                var possibleEnvPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Exwhyzee.Messaging.Web", ".env")
                };

                foreach (var envPath in possibleEnvPaths)
                {
                    if (File.Exists(envPath))
                    {
                        foreach (var rawLine in File.ReadAllLines(envPath))
                        {
                            var line = rawLine.Trim();
                            if (line.StartsWith(keyName + "="))
                            {
                                return line.Substring((keyName + "=").Length).Trim();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}