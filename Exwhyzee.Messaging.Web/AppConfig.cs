using System.Configuration;

namespace Exwhyzee.Messaging.Web
{
    public class AppConfig
    {
        public static string PayStackSecretKey
        {
            get { return System.Configuration.ConfigurationManager.AppSettings["PayStackSecretKey"]; }
        }
    }
}