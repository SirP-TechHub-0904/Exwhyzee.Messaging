using System;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Services;

namespace Exwhyzee.Messaging.Core.Data.Services
{
    public class SendEmail : ISendEmail
    {
        private readonly IZeptoMailService _zeptoMailService;

        public SendEmail()
        {
            _zeptoMailService = new ZeptoMailService();
        }

        public SendEmail(IZeptoMailService zeptoMailService)
        {
            _zeptoMailService = zeptoMailService ?? new ZeptoMailService();
        }

        public Task<bool> SendEmailAsync(string message, string recipient, string subject)
        {
            return _zeptoMailService.SendEmailAsync(message, recipient, subject);
        }
    }
}
