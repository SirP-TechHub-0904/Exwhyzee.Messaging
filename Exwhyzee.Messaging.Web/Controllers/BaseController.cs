using Exwhyzee.Messaging.Core.Models;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;

namespace Exwhyzee.Messaging.Web.Controllers
{
    public class BaseController : Controller
    {
        private Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        

        public BaseController() { }
        public BaseController(Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> UserManager
        {
            get => _userManager ?? HttpContext?.RequestServices?.GetService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
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


