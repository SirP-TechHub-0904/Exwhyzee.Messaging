using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Dtos;
using Exwhyzee.Messaging.Web.Services;
using Exwhyzee.Messaging.Web.Models;
using Hangfire;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Net.Mail;

namespace Exwhyzee.Messaging.Web.Controllers
{

    public class MailClass
    {
        public string Email { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
    }

    [Authorize]
    [RoutePrefix("api/ApiXyzSms")]
    public class ApiXyzSmsController : ApiController
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationRoleManager _roleManager;
        private IClientService _clientService = new ClientService();
        private ITransactionService _transactions = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private IPaystackTransactionService _paystackTransactionService = new PaystackTransactionService();
        private System.Random randomInteger = new System.Random();

        public ApiXyzSmsController()
        {
        }

        public ApiXyzSmsController(ISecureDataFormat<AuthenticationTicket> accessTokenFormat, ApplicationUserManager userManager, ApplicationSignInManager signInManager, ApplicationRoleManager roleManager)
        {
            AccessTokenFormat = accessTokenFormat;
            SignInManager = signInManager;
            RoleManager = roleManager;
        }
        public ApiXyzSmsController(ClientService clientService)
        {
            _clientService = clientService;
        }

        public ApiXyzSmsController(PaystackTransactionService paystackTransactionService)
        {
            _paystackTransactionService = paystackTransactionService;
        }

        //dashboard controller
        public ApiXyzSmsController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }
        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? Request.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationRoleManager RoleManager
        {
            get
            {
                return _roleManager ?? Request.GetOwinContext().Get<ApplicationRoleManager>();
            }
            private set
            {
                _roleManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        private const string LocalLoginProvider = "Local";

        public ISecureDataFormat<AuthenticationTicket> AccessTokenFormat { get; private set; }



        // POST api/Account/Logout
        [Route("Logout")]
        public IHttpActionResult Logout()
        {
            Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return Ok();
        }



        // POST api/Account/ChangePassword
        [Route("ChangePassword")]
        public async Task<IHttpActionResult> ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword,
                model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/SetPassword
        [Route("SetPassword")]
        public async Task<IHttpActionResult> SetPassword(SetPasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }


        // POST api/Account/RemoveLogin
        [Route("RemoveLogin")]
        public async Task<IHttpActionResult> RemoveLogin(RemoveLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result;

            if (model.LoginProvider == LocalLoginProvider)
            {
                result = await UserManager.RemovePasswordAsync(User.Identity.GetUserId());
            }
            else
            {
                result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(),
                    new UserLoginInfo(model.LoginProvider, model.ProviderKey));
            }

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }


        // POST api/Account/Register
        [AllowAnonymous]
        [Route("Register")]
        public async Task<IHttpActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApplicationUser() { UserName = model.Username, Email = model.Email };
            //Add Other properties
            user.PhoneNumber = model.PhoneNumber;
            user.DateRegitered = DateTime.UtcNow.AddHours(1);
            user.DateOfBirth = DateTime.ParseExact(model.DateOfBirth.ToString(), "MM/dd/yyyy", null);
            user.EmailConfirmed = true;
            var result = await UserManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await UserManager.AddToRoleAsync(user.Id, "Client");
                var u = await UserManager.FindByNameAsync(user.UserName);
                return Ok();
            }
            return GetErrorResult(result);

        }

        // POST: /Account/Login
        //send message
        [AllowAnonymous]
        [Route("ComposeMessage")]
        [HttpGet]
        public async Task<IHttpActionResult> ComposeMessage(string username, string password, string recipients, string senderId, string smsmessage, string smssendoption, string scheduledate = "")
        {
            HttpResponseMessage responseMessage;
            var user = await UserManager.FindByNameAsync(username);
            if (user == null)
            {
                responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "137 : Unable to fetch User or Invalid User");
                return ResponseMessage(responseMessage);
            }

            var result = await SignInManager.PasswordSignInAsync(username, password, false, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    var client = await _clientService.GetClientDetailsByUserId(user.Id);
                    string userId = user.Id;
                    DateTime scheduleDate = DateTime.Now;
                    if (!String.IsNullOrEmpty(scheduledate))
                    {
                        scheduleDate = DateTime.ParseExact(scheduledate.ToString(), "dd/MM/yyyy hh:mm", null);
                    }

                    ///MM/dd/yyyy
                    ///Reading Contacts
                    ///

                    if (recipients != null)
                    {
                        if (recipients.Substring(recipients.Length - 1) == ",")
                        {
                            recipients = recipients.Remove(recipients.Length - 1);
                        }
                    }



                    if (string.IsNullOrEmpty(recipients))
                    {
                        //return String("Message Sending failed. No recipient was added or selected.");

                    }
                    //get page count
                    int pageCount = SmsServices.CountPage(smsmessage);

                    //Remove duplicate Numbers
                    List<string> numbers = new List<string>(SmsServices.RemoveDuplicates(recipients));

                    //Format Numbers with International dail codes
                    List<string> fNumbers = new List<string>(SmsServices.FormatNumbers(numbers.ToList()));

                    //units needed per page
                    decimal units = SmsServices.UnitsPerPage(fNumbers.ToList());

                    //total units needed
                    decimal totalUnitsNeeded = pageCount * units;

                    //Check if Client's Unit is sufficient
                    if (totalUnitsNeeded > client.Units)
                    {
                        //ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                        //TempData["error"] = "Sending Message failed. You have insufficient unit balance. Your current balance is " + client.Units + "units, while total units required is " + totalUnitsNeeded + ".";
                        //return View(model);
                    }

                    //Save message to History
                    Message message = new Message();
                    message.DeliveredDate = DateTime.UtcNow;
                    message.MessageContent = smsmessage;
                    message.Recipients = string.Join(",", fNumbers.ToList());
                    message.Response = "Pending";
                    message.SenderId = senderId.ToString();
                    //message.Scheduleddate = Convert.ToDateTime(model.ScheduleDate)/*;*/
                    if (!String.IsNullOrEmpty(scheduledate))
                    {
                        message.Scheduleddate = DateTime.ParseExact(scheduledate.ToString(), "dd/MM/yyyy hh:mm", null);

                    }

                    if (smssendoption == "SendLater")
                    {
                        message.Status = MessageStatus.Scheduled;
                    }
                    else if (smssendoption == "SaveDraft")
                    {
                        message.Status = MessageStatus.Draft;
                    }
                    else
                    {
                        message.Status = MessageStatus.Pending;
                    }

                    message.UnitsUsed = 0;
                    message.UserId = User.Identity.GetUserId();

                    await _clientService.AddMessageToHistory(message);

                    if (smssendoption == "SendNow")
                    {
                        var response = await _clientService.SendSmsById(message.MessageId, totalUnitsNeeded);
                        var sentMessage = await _clientService.GetMessage(message.MessageId);
                        if (response.status.ToLower().Contains("success"))
                        {
                            //Update Client
                            client.Units = client.Units - totalUnitsNeeded;
                            await _clientService.UpdateClient(client);

                            sentMessage.UnitsUsed = totalUnitsNeeded;
                            sentMessage.Status = MessageStatus.Sent;
                            sentMessage.Response = response.msg;
                            sentMessage.Response_status = response.status;
                            sentMessage.Response_error_code = response.error_code;
                            sentMessage.Response_cost = response.cost;
                            sentMessage.Response_data = response.data;
                            sentMessage.Response_msg = response.msg;
                            sentMessage.Response_length = response.length;
                            sentMessage.Response_page = response.page;
                            sentMessage.Response_balance = response.balance;
                            sentMessage.Response_BalanceResponse = response.BalanceResponse;
                            await _clientService.UpdateMessageStatus(sentMessage);

                            return Ok("OK: response " + sentMessage.UnitsUsed + " units");
                        }
                        else
                        {
                            //ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                            //TempData["error"] = response + ". Sending Message Failed. Please try again or Contact the Administrator.";
                            //return View(model);
                        }
                    }
                    else if (smssendoption == "SendLater")
                    {
                        DateTime currentTime = DateTime.UtcNow.AddHours(1);
                        TimeSpan elapsedTime = scheduleDate.Subtract(currentTime);

                        BackgroundJob.Schedule(() => SendLater(message.MessageId, client.ClientId, totalUnitsNeeded), TimeSpan.FromMinutes(elapsedTime.TotalMinutes));
                        return Ok("OK: Message has been scheduled to send in " + elapsedTime.Minutes + "mins");


                    }
                    else if (smssendoption == "SaveDraft")
                    {
                        return Ok("OK: Message has been saved as Draft successfully");

                        //131 = "Message has been saved as Draft successfully.";

                    }
                    responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "132 : Sending Message Failed. Please Contact the Administrator");
                    return ResponseMessage(responseMessage);

                //132"Sending Message Failed. Please Contact the Administrator.";



                case SignInStatus.Failure:

                default:

                    break;
            }

            responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "133");
            return ResponseMessage(responseMessage);
            //133"Sending Message Failed. Try Again.";
        }


        //get balance unit

        [AllowAnonymous]
        [Route("CheckBalance")]
        [HttpGet]
        public async Task<IHttpActionResult> CheckBalance(string username, string password, bool balance)
        {
            HttpResponseMessage responseMessage;
            var user = await UserManager.FindByNameAsync(username);
            if (user == null)
            {
                responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "137 : Unable to fetch User or Invalid User");
                return ResponseMessage(responseMessage);
            }

            var result = await SignInManager.PasswordSignInAsync(username, password, false, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    var client = await _clientService.GetClientDetailsByUserId(user.Id);
                    string userId = user.Id;
                    if (balance == false)
                    {
                        responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "138 : Unable to fetch Unit. Balance is false");
                        return ResponseMessage(responseMessage);
                    }
                    if (client != null)
                    {
                        return Ok(client.Units.ToString());
                    }
                    else
                    {
                        responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "136 : Unable to fetch Unit");
                        return ResponseMessage(responseMessage);
                    }


                //132"Sending Message Failed. Please Contact the Administrator.";



                case SignInStatus.Failure:

                default:

                    break;
            }

            responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "133");
            return ResponseMessage(responseMessage);
            //133"Sending Message Failed. Try Again.";
        }

        [AllowAnonymous]
        [Route("MessageHistory")]
        [HttpGet]
        public async Task<IEnumerable<MessageDto>> MessageHistory(string username, string password, bool history)
        {

            var user = await UserManager.FindByNameAsync(username);
            if (user == null)
            {
                throw new HttpResponseException(Request.CreateResponse(HttpStatusCode.NotFound));
            }

            var result = await SignInManager.PasswordSignInAsync(username, password, false, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    var client = await _clientService.GetClientDetailsByUserId(user.Id);
                    string userId = user.Id;

                    if (history == false)
                    {
                        throw new HttpResponseException(Request.CreateResponse(HttpStatusCode.NotFound));
                    }
                    if (client != null)
                    {
                        var msg = await _clientService.GetClientMessageHistory(user.Id);

                        var messages = msg.Select(r => new MessageDto()
                        {
                            MessageId = r.MessageId,
                            SenderId = r.SenderId,
                            DeliveredDate = r.DeliveredDate,
                            RecipientsCount = GeneralServices.NumberCount(r.Recipients),
                            Scheduleddate = r.Scheduleddate,
                            Status = r.Status,
                            UnitsUsed = r.UnitsUsed
                        }).ToList();

                        return messages;
                    }
                    else
                    {
                        throw new HttpResponseException(Request.CreateResponse(HttpStatusCode.NotFound));
                    }

                //132"Sending Message Failed. Please Contact the Administrator.";



                case SignInStatus.Failure:

                default:

                    break;
            }
            throw new HttpResponseException(Request.CreateResponse(HttpStatusCode.NotFound));

        }


        [AllowAnonymous]
        [Route("MessageDetails")]
        [HttpGet]
        public async Task<IHttpActionResult> MessageDetails(string username, string password, int messageId = 0)
        {
            HttpResponseMessage responseMessage;
            var user = await UserManager.FindByNameAsync(username);
            if (user == null)
            {
                responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "137 : Unable to fetch User or Invalid User");
                return ResponseMessage(responseMessage);
            }

            var result = await SignInManager.PasswordSignInAsync(username, password, false, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    var client = await _clientService.GetClientDetailsByUserId(user.Id);
                    string userId = user.Id;
                    if (messageId == 0)
                    {
                        responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "138 : invalid mesage Id");

                        return ResponseMessage(responseMessage);
                    }
                    if (client != null)
                    {
                        Message message = await _clientService.GetMessage(messageId);

                        var output = new MessageDetailDto
                        {
                            MessageId = message.MessageId,
                            SenderId = message.SenderId,
                            Recipients = message.Recipients,
                            RecipientsCount = GeneralServices.NumberCount(message.Recipients),
                            MessageContent = message.MessageContent,
                            Response = message.Response,
                            UnitsUsed = message.UnitsUsed,
                            Scheduleddate = message.Scheduleddate,
                            DeliveredDate = message.DeliveredDate,
                            Status = message.Status,
                            Username = user.UserName
                        };
                        return Ok(output);
                    }
                    else
                    {
                        responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "136 : Unable to fetch Unit");
                        return ResponseMessage(responseMessage);
                    }


                //132"Sending Message Failed. Please Contact the Administrator.";



                case SignInStatus.Failure:

                default:

                    break;
            }

            responseMessage = Request.CreateResponse(HttpStatusCode.BadRequest, "133");
            return ResponseMessage(responseMessage);
            //133"Sending Message Failed. Try Again.";
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

        #region Helpers

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }
            public string ProviderKey { get; set; }
            public string UserName { get; set; }

            public IList<Claim> GetClaims()
            {
                IList<Claim> claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.NameIdentifier, ProviderKey, null, LoginProvider));

                if (UserName != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, UserName, null, LoginProvider));
                }

                return claims;
            }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || String.IsNullOrEmpty(providerKeyClaim.Issuer)
                    || String.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirstValue(ClaimTypes.Name)
                };
            }
        }

        private static class RandomOAuthStateGenerator
        {
            private static RandomNumberGenerator _random = new RNGCryptoServiceProvider();

            public static string Generate(int strengthInBits)
            {
                const int bitsPerByte = 8;

                if (strengthInBits % bitsPerByte != 0)
                {
                    throw new ArgumentException("strengthInBits must be evenly divisible by 8.", "strengthInBits");
                }

                int strengthInBytes = strengthInBits / bitsPerByte;

                byte[] data = new byte[strengthInBytes];
                _random.GetBytes(data);
                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        #endregion

        public async Task SendLater(int messageId, int clientId, decimal totalUnitsNeeded)
        {
            var client = await _clientService.GetClientDetails(clientId);
            var sentMessage = await _clientService.GetMessage(messageId);

            //Check if Client's Unit is sufficient
            if (totalUnitsNeeded > client.Units)
            {
                throw new Exception("You don't have enough Units for this transaction.");
            }
            else
            {
                var response = await _clientService.SendSmsById(messageId, totalUnitsNeeded);
                if (response.status.ToLower().Contains("success"))
                {
                    //Update Client
                    client.Units = client.Units - totalUnitsNeeded;
                    await _clientService.UpdateClient(client);

                    sentMessage.UnitsUsed = totalUnitsNeeded;
                    sentMessage.Status = MessageStatus.Sent;
                    sentMessage.Response = response.msg;
                    sentMessage.Response_status = response.status;
                    sentMessage.Response_error_code = response.error_code;
                    sentMessage.Response_cost = response.cost;
                    sentMessage.Response_data = response.data;
                    sentMessage.Response_msg = response.msg;
                    sentMessage.Response_length = response.length;
                    sentMessage.Response_page = response.page;
                    sentMessage.Response_balance = response.balance;
                    sentMessage.Response_BalanceResponse = response.BalanceResponse;
                    await _clientService.UpdateMessageStatus(sentMessage);
                }
                else
                {
                    throw new Exception("Message Sending failed.");
                }
            }
        }

        [AllowAnonymous]
        [Route("Sendmail")]
        [HttpPost]
        public async Task<IHttpActionResult> Sendmail(MailClass M)
        {

            try
            {
                //create the mail message 
                MailMessage mail = new MailMessage();
                //set the addresses 
                mail.From = new MailAddress("peteronwuka@exwhyzee.ng"); //IMPORTANT: This must be same as your smtp authentication address.
                mail.To.Add(M.Email);

                //set the content 
                mail.Subject = M.Title;
                mail.Body = M.Message;
                mail.IsBodyHtml = true;
                //send the message 
                SmtpClient smtp = new SmtpClient("mail.exwhyzee.ng");

                //IMPORANT:  Your smtp login email MUST be same as your FROM address. 
                NetworkCredential Credentials = new NetworkCredential("peteronwuka@exwhyzee.ng", "nation@123");
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = Credentials;
                smtp.Port = 25;    //alternative port number is 8889
                smtp.Timeout = 300000;
                smtp.EnableSsl = false;
                smtp.Send(mail);
                return Ok("true");
            }
            catch (Exception d)
            {
                return Ok(d);
            }


        }
    }



    //// GET: api/ApiXyzSms
    //public IEnumerable<string> Get()
    //{
    //    return new string[] { "value1", "value2" };
    //}

    //// GET: api/ApiXyzSms/5
    //public string Get(int id)
    //{
    //    return "value";
    //}

    //// POST: api/ApiXyzSms
    //public void Post([FromBody]string value)
    //{
    //}

    //// PUT: api/ApiXyzSms/5
    //public void Put(int id, [FromBody]string value)
    //{
    //}

    //// DELETE: api/ApiXyzSms/5
    //public void Delete(int id)
    //{
    //}

}
