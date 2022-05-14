using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel
{
    public class AdminpanelAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "Adminpanel";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Adminpanel_default",
                "Adminpanel/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}