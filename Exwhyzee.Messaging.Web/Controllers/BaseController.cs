using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Controllers
{
    public class BaseController : Controller
    {
        private ApplicationUserManager _userManager;

        public BaseController()
        {
        }

        public BaseController(ApplicationUserManager userManager)
        {
            _userManager = userManager;
        }

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set
            {
                _userManager = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }
    }
}