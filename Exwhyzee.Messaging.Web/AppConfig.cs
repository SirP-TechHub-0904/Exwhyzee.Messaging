using System;
using Microsoft.Extensions.Configuration;

namespace Exwhyzee.Messaging.Web
{
    public class AppConfig
    {
        public static IConfiguration Configuration { get; set; }

        public static string PayStackSecretKey =>
            Configuration?["PaystackSettings:SecretKey"] ??
            Configuration?["AppSettings:PayStackSecretKey"] ??
            Configuration?["PayStackSecretKey"] ??
            "sk_live_your_paystack_secret_key_here";

        public static string PayStackPublicKey =>
            Configuration?["PaystackSettings:PublicKey"] ??
            Configuration?["AppSettings:PayStackPublicKey"] ??
            Configuration?["PayStackPublicKey"] ??
            "pk_live_your_paystack_public_key_here";
    }
}