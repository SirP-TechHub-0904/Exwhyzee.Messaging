using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using System.Threading.Tasks;
using System.Web;
using Exwhyzee.Messaging.Web.Models;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Data.Entity;
using Exwhyzee.Messaging.Web.ViewModels;
using Exwhyzee.Messaging.Web.Services;
using Newtonsoft.Json;

namespace Exwhyzee.Messaging.Web.Controllers
{

    public class ComfirmAccount
    {
        public string Code { get; set; }
        public string Username { get; set; }
    }
    public class UserAccount
    {
        public string Username { get; set; }
    }
    public class MessageData
    {
        public string Username { get; set; }
        public int MessageId { get; set; }
    }

    public class PasswordChange
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
        public string Username { get; set; }
    }
    public class AppController : ApiController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationRoleManager _roleManager;
        private IClientService _clientService = new ClientService();
        private ITransactionService _transactions = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private IPaystackTransactionService _paystackTransactionService = new PaystackTransactionService();
        private System.Random randomInteger = new System.Random();
        public AppController()
        {
        }


        public AppController(ISecureDataFormat<AuthenticationTicket> accessTokenFormat, ApplicationUserManager userManager, ApplicationSignInManager signInManager, ApplicationRoleManager roleManager, PaystackTransactionService paystackTransactionService, ClientService clientService, DashboardService dashboardService)
        {

            SignInManager = signInManager;
            RoleManager = roleManager;
            _paystackTransactionService = paystackTransactionService; _dashboardService = dashboardService; _clientService = clientService;
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

        [AllowAnonymous]
        [Route("PasswordChange")]
        public async Task<HttpResponseMessage> PasswordChange([FromBody] PasswordChange model)
        {


            try
            {

                var data = await UserManager.FindByNameAsync(model.Username);
                if(data == null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "Invalid User");
                }
                var checkpassword = await UserManager.CheckPasswordAsync(data, model.OldPassword);
                if(checkpassword == false)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "Old Password is Wrong");
                }
                if(model.OldPassword != model.NewPassword)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "Password Don't Match");
                }
                var change = await UserManager.ChangePasswordAsync(data.Id, model.OldPassword, model.NewPassword);
                if (change.Succeeded)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "Password Change Successful.");
                }
                return Request.CreateResponse(HttpStatusCode.OK, change);
            }
            catch (Exception c)
            {

            }

            return Request.CreateResponse(HttpStatusCode.OK, "Invalid Request");
        }


        [AllowAnonymous]
        [Route("Register")]
        public async Task<HttpResponseMessage> Register([FromBody] RegisterViewModel model)
        {


            try
            {
                var user = new ApplicationUser { UserName = model.Username, Email = model.Email };

                //Add Other properties
                user.PhoneNumber = model.PhoneNumber;
                user.DateRegitered = DateTime.UtcNow.AddHours(1);
                user.DateOfBirth = DateTime.ParseExact(model.DateOfBirth.ToString(), "MM/dd/yyyy", null);
                var result = await UserManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await UserManager.AddToRoleAsync(user.Id, "Client");
                    Guid obj = Guid.NewGuid();
                    string number = obj.ToString();
                    string codecheck = number.Replace("-", "");
                    string code = codecheck.ToUpper().Substring(0, 6);

                    var useradd = await UserManager.FindByIdAsync(user.Id);
                    useradd.Code = code;
                    await UserManager.UpdateAsync(useradd);

                    await UserManager.SendEmailAsync(user.Id, "Confirm your account", "Enter the code to confirm your account " + string.Format(code));
                    //  return RedirectToAction("GoToMail", new { id = user.Id });
                    return Request.CreateResponse(HttpStatusCode.OK, "Account Created awaiting confirmation");
                }
            }
            catch (Exception c)
            {

            }


            return Request.CreateResponse(HttpStatusCode.OK, "Invalid Request");
        }

        [AllowAnonymous]
        [Route("ComfirmAccount")]
        public async Task<HttpResponseMessage> ComfirmAccount([FromBody] ComfirmAccount model)
        {

            try
            {
                var settings = db.AdminSettings.FirstOrDefault();
                var useradd = await UserManager.FindByNameAsync(model.Username);
                if (useradd == null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "no user found");
                }
                if (useradd.Code == model.Code)
                {
                    var client = await _clientService.GetClientDetailsByUserId(useradd.Id);
                    if (client == null)
                    {
                        Client newClient = new Client();
                        newClient.UserId = useradd.Id;
                        newClient.Units = settings.UnitPerNewMember;
                        db.Clients.Add(newClient);
                        await db.SaveChangesAsync();
                        return Request.CreateResponse(HttpStatusCode.OK, "Account Confirmed");
                    }

                }

                return Request.CreateResponse(HttpStatusCode.OK, "wrong code");
            }

            catch (Exception c)
            {

            }


            return Request.CreateResponse(HttpStatusCode.OK, "Invalid Request");
        }


        [AllowAnonymous]
        [Route("UnitBalance")]
        public async Task<HttpResponseMessage> UnitBalance([FromBody] UserAccount model)
        {

            try
            {
                var user = await UserManager.FindByNameAsync(model.Username);
                var balance = await db.Clients.FirstOrDefaultAsync(x => x.UserId == user.UserName);
                string balancea = balance.Units.ToString();
                return Request.CreateResponse(HttpStatusCode.OK, balancea);
            }
            catch (Exception f)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "invalid");
            }
        }

        [AllowAnonymous]
        [Route("Profile")]
        public async Task<HttpResponseMessage> Profile([FromBody] UserAccount model)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(model.Username);
                var profile = await db.Clients.Include(x => x.User).FirstOrDefaultAsync(x => x.UserId == user.UserName);

                return Request.CreateResponse(HttpStatusCode.OK, profile);
            }
            catch (Exception f)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "invalid");
            }
        }

        [AllowAnonymous]
        [Route("SendMessage")]
        public async Task<HttpResponseMessage> SendMessage([FromBody] ComposeViewModel model)
        {
            DateTime scheduleDate = DateTime.Now;
            if (model.ScheduleDate != null)
            {
                scheduleDate = DateTime.ParseExact(model.ScheduleDate.ToString(), "dd/MM/yyyy hh:mm", null);
            }
            var user = await UserManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "invalid user");
            }
            var client = await db.Clients.FirstOrDefaultAsync(x => x.UserId == user.Id);
            //get page count
            int pageCount = SmsServices.CountPage(model.Content);

            //Remove duplicate Numbers
            List<string> numbers = new List<string>(SmsServices.RemoveDuplicates(model.Recipients));

            //Format Numbers with International dail codes
            List<string> fNumbers = new List<string>(SmsServices.FormatNumbers(numbers.ToList()));

            //units needed per page
            decimal units = SmsServices.UnitsPerPage(fNumbers.ToList());

            //total units needed
            decimal totalUnitsNeeded = pageCount * units;

            //Check if Client's Unit is sufficient
            if (totalUnitsNeeded > client.Units)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "You have insufficient unit balance. Your current balance is " + client.Units + "units, while total units required is " + totalUnitsNeeded + ".");

            }

            //Save message to History
            Message message = new Message();
            message.DeliveredDate = DateTime.UtcNow;
            message.MessageContent = model.Content;
            message.Recipients = string.Join(",", fNumbers.ToList());
            message.Response = "Pending";
            message.SenderId = model.SenderId.ToString();
            //message.Scheduleddate = Convert.ToDateTime(model.ScheduleDate)/*;*/
            if (message.Scheduleddate != null)
            {
                message.Scheduleddate = DateTime.ParseExact(model.ScheduleDate.ToString(), "dd/MM/yyyy hh:mm", null);

            }

            if (model.SendOption == "SendLater")
            {
                message.Status = MessageStatus.Scheduled;
            }
            else if (model.SendOption == "SaveDraft")
            {
                message.Status = MessageStatus.Draft;
            }
            else
            {
                message.Status = MessageStatus.Pending;
            }

            message.UnitsUsed = 0;
            message.UserId = user.Id;

            await _clientService.AddMessageToHistory(message);

            if (model.SendOption == "SendNow")
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

                    return Request.CreateResponse(HttpStatusCode.OK, "Message has been sent successfully. Total Units used is " + totalUnitsNeeded + ".");
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.OK, response + ". Sending Message Failed. Please try again or Contact the Administrator.");
                }
            }
            else if (model.SendOption == "SendLater")
            {
                DateTime currentTime = DateTime.UtcNow.AddHours(1);
                TimeSpan elapsedTime = scheduleDate.Subtract(currentTime);

                return Request.CreateResponse(HttpStatusCode.OK, "Message has been scheduled to send in " + elapsedTime.Minutes + "mins");

            }
            else if (model.SendOption == "SaveDraft")
            {
                return Request.CreateResponse(HttpStatusCode.OK, "Message has been saved as Draft successfully.");

            }


            return Request.CreateResponse(HttpStatusCode.OK, "Invalid.");
        }


        [AllowAnonymous]
        [Route("GetMessageById")]
        public async Task<HttpResponseMessage> GetMessageById(int MessageId)
        {
            try
            {
                var message = await db.Messages.Include(x => x.User).FirstOrDefaultAsync(x => x.MessageId == MessageId);
                return Request.CreateResponse(HttpStatusCode.OK, message);
            }
            catch(Exception d)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "invalid");
            }
        }

        [AllowAnonymous]
        [Route("GetMessage")]
        public async Task<HttpResponseMessage> GetMessage([FromUri] PagingParameterModel pagingparametermodel)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(pagingparametermodel.Username);
                if (user == null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "invalid user");
                }
                // Return List of Customer  
                var source = (from message in db.Messages.Include(x=>x.User).Where(x=>x.UserId == user.Id)
                                .OrderBy(a => a.DeliveredDate)
                              select message).AsQueryable();

                // Get's No of Rows Count   
                int count = source.Count();

                // Parameter is passed from Query string if it is null then it default Value will be pageNumber:1  
                int CurrentPage = pagingparametermodel.pageNumber;

                // Parameter is passed from Query string if it is null then it default Value will be pageSize:20  
                int PageSize = pagingparametermodel.pageSize;

                // Display TotalCount to Records to User  
                int TotalCount = count;

                // Calculating Totalpage by Dividing (No of Records / Pagesize)  
                int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

                // Returns List of Customer after applying Paging   
                var items = source.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

                // if CurrentPage is greater than 1 means it has previousPage  
                var previousPage = CurrentPage > 1 ? "Yes" : "No";

                // if TotalPages is greater than CurrentPage means it has nextPage  
                var nextPage = CurrentPage < TotalPages ? "Yes" : "No";

                // Object which we are going to send in header   
                var paginationMetadata = new
                {
                    totalCount = TotalCount,
                    pageSize = PageSize,
                    currentPage = CurrentPage,
                    totalPages = TotalPages,
                    previousPage,
                    nextPage
                };

                // Setting Header  
                HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
                // Returing List of Customers Collections  
                          return Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch (Exception d)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "invalid");
            }
        }


        [AllowAnonymous]
        [Route("GetTransactions")]
        public async Task<HttpResponseMessage> GetTransactions([FromUri] PagingParameterModel pagingparametermodel)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(pagingparametermodel.Username);
                if (user == null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, "invalid user");
                }
                // Return List of Customer  
                var source = (from message in db.Transactions.Include(x => x.User).Where(x => x.UserId == user.Id)
                                .OrderBy(a => a.DateCreated)
                              select message).AsQueryable();

                // Get's No of Rows Count   
                int count = source.Count();

                // Parameter is passed from Query string if it is null then it default Value will be pageNumber:1  
                int CurrentPage = pagingparametermodel.pageNumber;

                // Parameter is passed from Query string if it is null then it default Value will be pageSize:20  
                int PageSize = pagingparametermodel.pageSize;

                // Display TotalCount to Records to User  
                int TotalCount = count;

                // Calculating Totalpage by Dividing (No of Records / Pagesize)  
                int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

                // Returns List of Customer after applying Paging   
                var items = source.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

                // if CurrentPage is greater than 1 means it has previousPage  
                var previousPage = CurrentPage > 1 ? "Yes" : "No";

                // if TotalPages is greater than CurrentPage means it has nextPage  
                var nextPage = CurrentPage < TotalPages ? "Yes" : "No";

                // Object which we are going to send in header   
                var paginationMetadata = new
                {
                    totalCount = TotalCount,
                    pageSize = PageSize,
                    currentPage = CurrentPage,
                    totalPages = TotalPages,
                    previousPage,
                    nextPage
                };

                // Setting Header  
                HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
                // Returing List of Customers Collections  
                return Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch (Exception d)
            {
                return Request.CreateResponse(HttpStatusCode.OK, "invalid");
            }
        }


    }
}