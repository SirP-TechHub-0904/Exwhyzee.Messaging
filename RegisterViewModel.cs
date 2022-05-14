using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Mobile.ViewModels
{
    public class RegisterViewModel
    {
      
        public string Username { get; set; }
        
        public string Email { get; set; }
        
        public string PhoneNumber { get; set; }
        
        public string DateOfBirth { get; set; }

        public DateTime DateRegitered { get; set; }

        public string Password { get; set; }
    }
}