using Exwhyzee.Messaging.Mobile.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace Exwhyzee.Messaging.Mobile.ViewModels
{
    public class RegisterViewModel
    {
        ApiServices _apiServices = new ApiServices();

        public string Username { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string DateOfBirth { get; set; }

        public DateTime DateRegitered { get; set; }

        public string Password { get; set; }

        public string ConfirmPassword { get; set; }
        public string Message { get; set; }

        public ICommand RegisterCommand
        {
            get
            {
                return new Command(async() =>
                {

                  var isSuccess = await _apiServices.RegisterAsync(Username, Email, PhoneNumber, DateOfBirth, DateRegitered, Password, ConfirmPassword);

                    if (isSuccess)
                        Message = "Registered successfully";
                    else
                    {
                        Message = "Retry";
                    }

                });
            }
        }
    }
}
