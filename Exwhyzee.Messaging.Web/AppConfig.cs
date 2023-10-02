using System.Configuration;

namespace Exwhyzee.Messaging.Web
{
    public class AppConfig
    {
        public static string PayStackSecretKey
        {
            get { return ConfigurationManager.AppSettings["PayStackSecretKey"]; }
        }
    }
}