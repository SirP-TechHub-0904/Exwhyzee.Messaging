using Exwhyzee.Messaging.Mobile.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Mobile.Services
{
    public class ApiServices
    {
        public async Task<bool> RegisterAsync(string username, string email, string phoneNumber, string dateOfBirth, DateTime dateRegitered, string password, string confirmPassword)
        {
            var client = new HttpClient();

            var model = new RegisterViewModel
            {
                Username = username,
                Email = email,
                DateOfBirth = dateOfBirth,
                DateRegitered = DateTime.UtcNow.AddHours(1),
                PhoneNumber = phoneNumber,
                Password = password,
                ConfirmPassword = confirmPassword

            };

            var json = JsonConvert.SerializeObject(model);
            HttpContent content = new StringContent(json);

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
           var response = client.PostAsync("http://localhost:49689/api/ApiXyzSms/Register", content);
            return response.IsCompleted;
        }
    }
}
