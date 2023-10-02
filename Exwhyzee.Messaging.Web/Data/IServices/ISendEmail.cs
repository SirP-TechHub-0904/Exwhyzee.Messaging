using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.IServices
{
    public interface ISendEmail
    {
        Task<bool> SendEmailAsync(string message, string recipient, string subject);
    }
}
