using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Exwhyzee.Messaging.Web.Startup))]
namespace Exwhyzee.Messaging.Web
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
