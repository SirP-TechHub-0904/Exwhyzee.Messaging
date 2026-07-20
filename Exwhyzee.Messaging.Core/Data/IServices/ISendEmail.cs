using Exwhyzee.Messaging.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Data.IServices
{
    public interface ISendEmail
    {
        Task<bool> SendEmailAsync(string message, string recipient, string subject);
    }
}
