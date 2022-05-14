using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.ViewModels
{
    public class UserViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public bool LockOutEnabled { get; set; }
    }
}