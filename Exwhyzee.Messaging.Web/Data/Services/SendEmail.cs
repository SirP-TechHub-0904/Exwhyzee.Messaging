using Exwhyzee.Messaging.Web.Data.IServices;
using System.Net.Mail;
using System.Net;
using System;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Exwhyzee.Messaging.Web.Data.Services
{
    public class SendEmail : ISendEmail
    {
        Task<bool> ISendEmail.SendEmailAsync(string message, string recipient, string subject)
        {
            try
            {

                MailMessage mail = new MailMessage();
 
                mail.Body = message;
                //set the addresses 
                mail.From = new MailAddress("support@xyzsms.com", "XYZ BULK SMS");
                mail.To.Add(recipient);

                //set the content 
                mail.Subject = subject;

                mail.IsBodyHtml = true;
                //send the message 
                SmtpClient smtp = new SmtpClient("mail.xyzsms.com");

                //IMPORANT:  Your smtp login email MUST be same as your FROM address. 
                NetworkCredential Credentials = new NetworkCredential("support@xyzsms.com", "ASD@1k123");
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = Credentials;
                smtp.Port = 25;    //alternative port number is 8889
                smtp.EnableSsl = false;
                smtp.Send(mail);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {

                return Task.FromResult(false);
            }
         }
    }
}