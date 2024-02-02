using Hangfire;
using Exwhyzee.Messaging.Web.Controllers;
using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Services;
using Exwhyzee.Messaging.Web.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using PagedList;
using Exwhyzee.Messaging.Web.PayStack;
using Exwhyzee.Messaging.Web.PayStack.Models;
using static Exwhyzee.Messaging.Web.Services.GeneralServices;

namespace Exwhyzee.Messaging.Web.Areas.ClientPanel.Controllers
{
    [Authorize(Roles = "Client")]
    public class DashboardController : BaseController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        private IClientService _clientService = new ClientService();
        private ISendEmail _email = new SendEmail();

        private IPayStackApi _paystack = new PayStackApi(AppConfig.PayStackSecretKey);
        private ITransactionService _transactions = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private IPaystackTransactionService _paystackTransactionService = new PaystackTransactionService();
        private System.Random randomInteger = new System.Random();

        public DashboardController()
        {
        }

        public DashboardController(ClientService clientService)
        {
            _clientService = clientService;
        }

        public DashboardController(PaystackTransactionService paystackTransactionService)
        {
            _paystackTransactionService = paystackTransactionService;
        }

        //dashboard controller
        public DashboardController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }
        [HttpGet, Tls]
        // GET: ClientPanel/Dashboard
        public async Task<ActionResult> Index()
        {
            //
            //Get Client By UserId
            var settings = db.AdminSettings.FirstOrDefault();
            var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            if (client == null)
            {
               // var josUser = await JosClient.GetJosUser(User.Identity.Name);
                Client newClient = new Client();
                //if (josUser != null)
                //{
                //    var josClient = await JosClient.GetJosClientById(josUser.id);
                //    string[] arr = josUser.name.Split(" ".ToCharArray(), 3, StringSplitOptions.RemoveEmptyEntries);

                //    if (arr.Length == 3)
                //    {
                //        newClient.FirstName = arr[0];
                //        newClient.Surname = arr[1];
                //        newClient.OtherNames = arr[2];
                //        db.Clients.Add(newClient);
                //        await db.SaveChangesAsync();
                //    }
                //    else if (arr.Length == 2)
                //    {
                //        newClient.FirstName = arr[0];
                //        newClient.Surname = arr[1];
                //        db.Clients.Add(newClient);
                //        await db.SaveChangesAsync();
                //    }
                //    else
                //    {
                //        newClient.FirstName = josUser.name;
                //    }
                //    newClient.UserId = User.Identity.GetUserId();
                //    newClient.Units = josClient.Units;
                //    db.Clients.Add(newClient);
                //    await db.SaveChangesAsync();

                //    Message msg = new Message();
                //    var cId = josUser.id;
                //    var messages = await JosClient.ListJosMessage();
                //    var usermessage = messages.Where(x => x.msgClientID == cId);
                //    var date = usermessage.Select(x => x.delivered);

                //    var ss = usermessage.Count();
                //    foreach (var m in usermessage)
                //    {
                //        //if (m.delivered != "0000-00-00")
                //        //{
                //        //    var date12 = Convert.ToDateTime(m.delivered);

                //        //    string datestring = date12.ToString("MM-dd-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                //        //    msg.DeliveredDate = Convert.ToDateTime(datestring);
                //        //}
                //        //else
                //        //{
                //        msg.DeliveredDate = DateTime.UtcNow;

                //        msg.MessageContent = m.message;
                //        msg.Recipients = m.recipients;
                //        msg.Response = m.apiResponse;
                //        if (m.schedule != null)
                //        {
                //            msg.Scheduleddate = Convert.ToDateTime(m.schedule);
                //        }
                //        msg.SenderId = m.senderID;
                //        msg.Status = MessageStatus.Sent;
                //        msg.UnitsUsed = m.unitsUsed;
                //        msg.UserId = User.Identity.GetUserId();
                //        msg.SummaryReport = m.delivery_report;
                //        db.Messages.Add(msg);
                //        await db.SaveChangesAsync();
                //    }

                //    var draft = await JosClient.ListJosDraftMessage();
                //    var userdraft = draft.Where(x => x.dftJxID == cId);
                //    foreach (var d in userdraft)
                //    {
                //        msg.UserId = User.Identity.GetUserId();
                //        msg.MessageContent = d.dftMessage;
                //        msg.SenderId = d.dftTitle;
                //        msg.Status = MessageStatus.Draft;
                //        msg.DeliveredDate = Convert.ToDateTime(d.dftCreated);
                //        db.Messages.Add(msg);
                //        await db.SaveChangesAsync();
                //    }
                //}
                //else
                //{
                    newClient.UserId = User.Identity.GetUserId();
                    newClient.Units = settings.UnitPerNewMember;
                    db.Clients.Add(newClient);
                    await db.SaveChangesAsync();
                


            }

            //client last ten messages
            var lastMessage = await _dashboardService.ClientLastMessages(10, User.Identity.GetUserId());
            ViewBag.LastTenMessage = lastMessage.ToArray();

            var userclient = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            //last ten transaction

            //var lastTenTransaction = await _dashboardService.ClientLastTransactions(client.ClientId);
            var transactions = db.Transactions.Where(x => x.ClientId == userclient.ClientId).OrderByDescending(x => x.TransactionId).Take(10).ToList();

            ViewBag.LastTenTransaction = transactions;




            //last ten scheduled messages
            var lastSchedule = await _dashboardService.ClientScheduledMessages(100, User.Identity.GetUserId());
            ViewBag.LastTenScheduledMessage = lastSchedule.Take(10).ToArray();

            //pending transactions

            var pendingTransactions = db.Transactions.Where(x => x.Status == TransactionStatus.Pending && x.ClientId == userclient.ClientId);
            ViewBag.pendingTransactions = pendingTransactions.Count();

            //client unit
            var clientUnit = userclient.Units;
            ViewBag.clientUnit = clientUnit;

            //transaction count
            ViewBag.ScheduleCount = lastSchedule.Count();

            //
            var modal = await db.ModalInfos.FirstOrDefaultAsync();
            ViewBag.Modal = modal;
            return View();
        }


        /// <summary>
        /// 
        /// 
        public ActionResult GetUserGroup()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> GetUserGroup(int JsonUserId, string username)
        {
            try
            {
                Group grp = new Group();
                var group = await JosClient.ListJosGroup();
                var userid = await db.Users.FirstOrDefaultAsync(x => x.UserName == username);
                var usergroup = group.Where(x => x.grpClientID == JsonUserId);
                foreach (var g in usergroup.ToList())
                {
                    grp.GpId = g.groupID;
                    grp.UserId = userid.Id;
                    grp.Name = g.groupName;
                    // grp.DateCreated = Convert.ToDateTime(g.grpCreated);
                    db.Groups.Add(grp);
                    await db.SaveChangesAsync();
                    //{"addID":"1","addClientID":"66","addGrpID":"1","addphone":"2347064656771","fullname":"","addCreated":"2011-12-13 15:32:17"},
                    //{"groupID":"1803","grpClientID":"63","groupName":"Priests","grpCreated":"2012-12-01 16:30:46"}, 
                }

                var allgroup = db.Groups.Where(x => x.UserId == userid.Id);
                foreach (var d in allgroup)
                {
                    var con = await JosClient.ListJosGroupContact();
                    var groupCon = con.Where(x => x.addGrpID == d.GpId);
                    if (groupCon.Count() > 0)
                    {
                        foreach (var c in groupCon.ToList())
                        {
                            Contact contact = new Contact();
                            contact.GpId = c.addGrpID;
                            contact.DateAddded = DateTime.UtcNow;
                            contact.PhoneNumber = c.addphone;
                            contact.GroupId = d.GroupId;
                            contact.Surname = c.fullname;
                            db.Contacts.Add(contact);
                            await db.SaveChangesAsync();
                        }


                    }

                }

                TempData["msgg"] = "successful";
                return RedirectToAction("GetUserGroup");
            }
            catch (Exception ex)
            {
                TempData["bad"] = "error";
                return RedirectToAction("GetUserGroup");
            }

        }
        /// </summary>
        /// <returns></returns>

        public ActionResult Compose()
        {
            string userId = User.Identity.GetUserId();
            var groups = db.Groups.OrderBy(x => x.Name).Where(x => x.Name != null && x.UserId == userId).Select(g => new
            {
                GroupId = g.GroupId,
                Name = g.Name
            }).ToList();
            ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Compose(ComposeViewModel model, int[] GroupId, HttpPostedFileBase file)
        {
            var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            string userId = User.Identity.GetUserId();
            DateTime scheduleDate = DateTime.Now;
            if (model.ScheduleDate != null)
            {
                scheduleDate = DateTime.ParseExact(model.ScheduleDate.ToString(), "dd/MM/yyyy hh:mm", null);
            }

            ///MM/dd/yyyy
            ///Reading Contacts from .txt file
            ///
            if (file != null && file.ContentLength > 0)
            {
                string directory = Server.MapPath("~/Uploads/Contacts/");
                int genNumber = randomInteger.Next(1000000000);
                string line;
                string numbers = "";
                if (file.FileName.ToLower().Contains("txt"))
                {
                    var fileName = Path.GetFileName(file.FileName);
                    file.SaveAs(Path.Combine(directory + genNumber + fileName));
                    line = System.IO.File.ReadAllText(directory + genNumber + fileName);
                    numbers = line.Replace("\r\n", ",").Replace(" ", ",");
                    // System.IO.File.Delete(directory + genNumber + fileName);
                }
                model.Recipients = model.Recipients + "," + numbers + ",";
            }

            var groups = db.Groups.OrderBy(x => x.Name).Where(x => x.Name != null && x.UserId == userId).Select(g => new
            {
                GroupId = g.GroupId,
                Name = g.Name
            }).ToList();
            if (GroupId != null)
            {
                string contacts;
                string combined = "";

                foreach (var item in GroupId)
                {
                    var itemContacts = db.Contacts.Where(x => x.GroupId == item).Select(x => x.PhoneNumber);
                    contacts = string.Join(",", itemContacts.ToList());
                    combined = combined + contacts + ",";

                }
                if (combined.Substring(combined.Length - 1) == ",")
                {
                    combined = combined.Remove(combined.Length - 1);
                }

                model.Recipients = model.Recipients + "," + combined;
            }
            if (model.Recipients != null)
            {
                if (model.Recipients.Substring(model.Recipients.Length - 1) == ",")
                {
                    model.Recipients = model.Recipients.Remove(model.Recipients.Length - 1);
                }
            }



            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.Recipients))
                {
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    ModelState.AddModelError("", "Message Sending failed. No recipient was added or selected. And any number to Recipient to save as Draft.");
                    return View(model);
                }
                //get page count
                int pageCount = SmsServices.CountPage(model.Content);

                //Remove duplicate Numbers
                List<string> numbers = new List<string>(SmsServices.RemoveDuplicates(model.Recipients));

                //Format Numbers with International dail codes
                List<string> fNumbers = new List<string>(SmsServices.FormatNumbers(numbers.ToList()));

                //units needed per page
                decimal units = SmsServices.UnitsPerPage(fNumbers.ToList());

                var nmb = string.Join(",", fNumbers.ToList());
                //total units needed
                decimal totalUnitsNeeded = pageCount * units;
                

                //Check if Client's Unit is sufficient
                if (totalUnitsNeeded > client.Units)
                {
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    TempData["error"] = "Sending Message failed. You have insufficient unit balance. Your current balance is " + client.Units + "units, while total units required is " + totalUnitsNeeded + ".";
                    return View(model);
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
                message.UserId = User.Identity.GetUserId();

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

                        ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                        TempData["success"] = "Message has been sent successfully. Total Units used is " + totalUnitsNeeded + ".";
                        return RedirectToAction("Compose");
                    }
                    else if(response.status.ToLower().Contains("error"))
                    {
                        if(response.error_code == "106")
                        {
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
                            TempData["error"] = "The sender ID used do not exist or has not been approved.";
                            ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                            return RedirectToAction("Compose");
                            
                        }
                    }
                    else if (response.status.ToLower().Contains("Blocked"))
                    {
                        if (response.error_code == "106")
                        {
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
                            TempData["error"] = response.msg;
                            ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                            return RedirectToAction("Compose");

                        }
                    }
                    else
                    {
                        ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                        TempData["error"] = "Sending Message Failed. Please try again or Contact the Administrator...";
                        return View(model);
                    }
                }
                else if (model.SendOption == "SendLater")
                {
                    DateTime currentTime = DateTime.UtcNow.AddHours(1);
                    TimeSpan elapsedTime = scheduleDate.Subtract(currentTime);

                    BackgroundJob.Schedule(() => SendLater(message.MessageId, client.ClientId, totalUnitsNeeded), TimeSpan.FromMinutes(elapsedTime.TotalMinutes));
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    TempData["success"] = "Message has been scheduled to send in " + elapsedTime.Minutes + "mins";
                    return RedirectToAction("Compose");
                }
                else if (model.SendOption == "SaveDraft")
                {
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    TempData["success"] = "Message has been saved as Draft successfully.";
                    return RedirectToAction("Compose");
                }
                //else
                //{
                //    await SendLater(message.MessageId, client.ClientId, totalUnitsNeeded);
                //    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                //    TempData["success"] = "Message has been sent successfully. Total Units used is " + totalUnitsNeeded + ".";
                //    return RedirectToAction("Compose");
                //}
                ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                TempData["error"] = "Sending Message Failed. Please Contact the Administrator.";
                return View(model);
            }
            ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
            return View(model);
        }

        //resend
        public async Task<ActionResult> Resend(int messegeId)
        {
            var model = await _clientService.GetMessage(messegeId);
            model.Resent = "Resend";
            await _clientService.UpdateMessageStatus(model);
            var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            string userId = User.Identity.GetUserId();



            //get page count
            int pageCount = SmsServices.CountPage(model.MessageContent);

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

                TempData["error"] = "Sending Message failed. You have insufficient unit balance. Your current balance is " + client.Units + "units, while total units required is " + totalUnitsNeeded + ".";
                return RedirectToAction("MessageDetails", new { id = model.MessageId });
            }

            //Save message to History
            Message message = new Message();
            message.DeliveredDate = DateTime.UtcNow;
            message.MessageContent = model.MessageContent;
            message.Recipients = string.Join(",", fNumbers.ToList());
            message.Response = "Pending";
            message.SenderId = model.SenderId.ToString();

            message.Status = MessageStatus.Pending;


            message.UnitsUsed = 0;
            message.UserId = model.UserId;

            await _clientService.AddMessageToHistory(message);

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


                TempData["success"] = "Message has been sent successfully. Total Units used is " + totalUnitsNeeded + ".";
                return RedirectToAction("MessageHistory");
            }
            else
            {

                TempData["error"] = response + ". Sending Message Failed. Please try again or Contact the Administrator.";
                return RedirectToAction("MessageDetails", new { id = model.MessageId });
            }



        }


        public async Task<ActionResult> SendDraft(int? id)
        {
            var message = await _clientService.GetMessage(id.Value);

            if (message != null)
            {
                ComposeViewModel model = new ComposeViewModel()
                {
                    Content = message.MessageContent,
                    Recipients = message.Recipients,
                    SenderId = message.SenderId
                };

                string userId = User.Identity.GetUserId();
                var groups = db.Groups.OrderBy(x => x.Name).Where(x => x.Name != null && x.UserId == userId).Select(g => new
                {
                    GroupId = g.GroupId,
                    Name = g.Name
                }).ToList();
                ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");

                return View(model);
            }

            return RedirectToAction("DraftMessages");
        }

        [HttpPost]
        public async Task<ActionResult> SendDraft(ComposeViewModel model, int[] GroupId, HttpPostedFileBase file, int? id)
        {
            var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            string userId = User.Identity.GetUserId();
            DateTime scheduleDate = DateTime.Now;
            if (model.ScheduleDate != null)
            {
                scheduleDate = DateTime.ParseExact(model.ScheduleDate.ToString(), "dd/MM/yyyy HH:mm", null);
            }

            ///MM/dd/yyyy
            ///Reading Contacts from .txt file
            ///
            if (file != null && file.ContentLength > 0)
            {
                string directory = Server.MapPath("~/Uploads/Contacts/");
                int genNumber = randomInteger.Next(1000000000);
                string line;
                if (file.FileName.ToLower().Contains("txt"))
                {
                    var fileName = Path.GetFileName(file.FileName);
                    file.SaveAs(Path.Combine(directory, genNumber + fileName));
                    line = System.IO.File.ReadAllText(directory + genNumber + fileName);
                    model.Recipients = line.Replace("\r\n", ",").Replace(" ", ",");
                    System.IO.File.Delete(directory + genNumber + fileName);
                }
            }

            var groups = db.Groups.OrderBy(x => x.Name).Where(x => x.Name != null && x.UserId == userId).Select(g => new
            {
                GroupId = g.GroupId,
                Name = g.Name
            }).ToList();
            if (GroupId != null)
            {
                string contacts;
                string combined = "";

                foreach (var item in GroupId)
                {
                    var itemContacts = db.Contacts.Where(x => x.GroupId == item).Select(x => x.PhoneNumber);
                    contacts = string.Join(",", itemContacts.ToList());
                    combined = combined + contacts;
                }

                model.Recipients = model.Recipients + combined;
            }

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.Recipients))
                {
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    ModelState.AddModelError("", "Message Sending failed. No recipient was added or selected.");
                    return View(model);
                }
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
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    TempData["error"] = "Sending Message failed. You have insufficient unit balance. Your current balance is " + client.Units + "units, while total units required is " + totalUnitsNeeded + ".";
                    return View(model);
                }

                //Save message to History
                var message = await _clientService.GetMessage(id.Value);
                message.DeliveredDate = DateTime.UtcNow;
                message.MessageContent = model.Content;
                message.Recipients = string.Join(",", fNumbers.ToList());
                message.Response = "Pending";
                message.SenderId = model.SenderId;
                if (model.SendOption == "SendLater")
                {
                    message.Status = MessageStatus.Scheduled;
                }
                else
                {
                    message.Status = MessageStatus.Pending;
                }

                message.UnitsUsed = 0;
                message.UserId = User.Identity.GetUserId();

                await _clientService.UpdateMessageStatus(message);

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

                        ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                        TempData["success"] = "Message has been sent successfully. Total Units used is " + totalUnitsNeeded + ".";
                        return RedirectToAction("Compose");
                    }
                }
                else if (model.SendOption == "SendLater")
                {
                    DateTime currentTime = DateTime.UtcNow.AddHours(1);
                    TimeSpan elapsedTime = scheduleDate.Subtract(currentTime);

                    BackgroundJob.Schedule(() => SendLater(message.MessageId, client.ClientId, totalUnitsNeeded), TimeSpan.FromMinutes(elapsedTime.TotalMinutes));
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    TempData["success"] = "Message has been scheduled to send in " + elapsedTime.Minutes + "mins";
                    return RedirectToAction("Compose");
                }
                else
                {
                    await SendLater(message.MessageId, client.ClientId, totalUnitsNeeded);
                    ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                    TempData["success"] = "Message has been sent successfully. Total Units used is " + totalUnitsNeeded + ".";
                    return RedirectToAction("Compose");
                }
                ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
                TempData["error"] = "Sending Message Failed. Please Contact the Administrator.";
                return View(model);
            }
            ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");
            return View(model);
        }

        // GET: ClientPanel/messagehistory
        public async Task<ActionResult> MessageHistory(string searchString, string currentFilter, int? page)
        {
            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }
            ViewBag.CurrentFilter = searchString;

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var msg = await _clientService.GetClientMessageHistory(user.Id);

            if (!String.IsNullOrEmpty(searchString))
            {

                msg = msg.Where(s => s.SenderId.ToUpper().Contains(searchString.ToUpper())).ToList();


            }

            int pageSize = 20;
            int pageNumber = (page ?? 1);

            return View(msg.OrderByDescending(x => x.DeliveredDate).ToPagedList(pageNumber, pageSize));
        }
         
        public async Task<ActionResult> DraftMessages()
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            return View(await _clientService.GetClientDraftMessages(user.Id));
        }

        public async Task<ActionResult> MessageDetails(int? id)
        {
            if (id == null)
            {
                id = 0;
            }
            Message message = await _clientService.GetMessage(id);

            if (message == null)
            {
                return HttpNotFound();
            }

            return View(message);
        }

        public async Task<ActionResult> TransactionHistory()
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            var transactions = await _transactions.GetTransactionsByClient(client.ClientId);

            return View(transactions);
        }

        public async Task<ActionResult> TransactionDetails(int Id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            var transactions = await _transactions.GetTransaction(Id);

            return View(transactions);
        }

        public ActionResult LoadVoucher()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> LoadVoucher(LoadVourcherViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await UserManager.FindByNameAsync(User.Identity.Name);
                    var voucher = await _clientService.CheckVoucher(model.Pin, model.BatchNumber);

                    if (voucher == null)
                    {
                        TempData["check"] = "Voucher pin does not exist. Please check the pin and try again";
                        return RedirectToAction("VoucherReport");
                    }

                    voucher.UserId = user.Id;
                    voucher.DateUsed = DateTime.UtcNow;
                    voucher.Status = VoucherStatus.Used;

                    await _clientService.LoadVoucher(voucher);
                    TempData["success"] = "Successfully";
                    TempData["unit"] = voucher.Units;
                    TempData["date"] = voucher.DateUsed;
                    return RedirectToAction("VoucherReport");
                }
                catch (Exception e)
                {
                    TempData["error"] = "Error: " + e.Message;
                    return View(model);
                }
            }

            TempData["error"] = "An Error occured";
            return View(model);
        }

        public ActionResult VoucherReport()
        {
            return View();
        }

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

        public async Task<ActionResult> EditClient()

        {
            var id = User.Identity.GetUserId();
            var clientedit = await _clientService.GetClientDetailsByUserId(id);
            if (clientedit == null)
            {
                return HttpNotFound();
            }

            return View(clientedit);
        }

        [HttpPost]
        public async Task<ActionResult> EditClient(Client item)

        {
            if (ModelState.IsValid)
            {
                await _clientService.UpdateClient(item);
                return RedirectToAction("Index");
            }

            return View(item);
        }

        public ActionResult BuyUnit()
        {
            ViewBag.PricePerUnit = db.AdminSettings.FirstOrDefault().PricePerUnit;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BuyUnit(BuyUnitsViewModel model)
        {
            model.PricePerUnit = db.AdminSettings.FirstOrDefault().PricePerUnit;
            if (ModelState.IsValid && model.Units > 0)
            {
                try
                {
                    var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
                    Transaction transaction = new Transaction();
                    transaction.Units = model.Units;
                    if (model.PaymentMethod == "OnlinePayment")
                    {
                        transaction.TransactionType = TransactionType.OnlinePayment;
                    }
                    else if (model.PaymentMethod == "BankDeposit")
                    {
                        transaction.TransactionType = TransactionType.BankDeposit;
                        transaction.GatewayResponse = "BANK DEPOSIT";
                    }

                    transaction.ClientId = client.ClientId;
                    transaction.Amount = model.PricePerUnit * model.Units;
                    await _transactions.AddTransaction(transaction);

                    //
                    //Redirect to the right page
                    if (model.PaymentMethod == "OnlinePayment")
                    {
                        return RedirectToAction("PayNow", new { id = transaction.TransactionId });
                    }
                    else if (model.PaymentMethod == "BankDeposit")
                    {
                        return RedirectToAction("Bankdetails", new { id = transaction.TransactionId });
                    }
                }
                catch (Exception e)
                {
                    TempData["error"] = "An Error occured. " + e.Message;
                    return View(model);
                }

                TempData["error"] = "An Error occured. ";
                return View(model);
            }

            TempData["error"] = "An Error occured. Units can't be less than Zero (0). ";
            return View(model);
        }

        public async Task<ActionResult> Bankdetails(int id = 0)
        {
            var transaction = await _transactions.GetTransaction(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }

            return View(transaction);
        }

        public async Task<ActionResult> PayNow(int id = 0)
        {
            ViewBag.PricePerUnit = db.AdminSettings.FirstOrDefault().PricePerUnit;
            var userid = User.Identity.GetUserId();
            var clientedit = await _clientService.GetClientDetailsByUserId(userid);
            var transaction = await _transactions.GetTransaction(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }

            int amountInKobo = (int)transaction.Amount * 100;
            var callbackUrl = Url.Action("Complete", "Dashboard", new { transactionid = id, area = "ClientPanel" }, protocol: Request.Url.Scheme);

            var response = await _paystack.Transactions.InitializeTransaction(clientedit.User.Email, amountInKobo,
                clientedit.FirstName, clientedit.Surname, callbackUrl, transaction.TransactionId.ToString(), false
                );
           

            if (response.status == true)
            {
                return Redirect(response.data.authorization_url);
            }

            return RedirectToAction("TransactionDetails", new { id = transaction.TransactionId });

        }

        public ActionResult _BankDetails()
        {
            var bank = db.BankDetails.Where(x => x.Active == true);
            ViewBag.BankDetails = bank.ToArray();
            return PartialView();
        }

        public async Task<ActionResult> Complete()
        {
            //
              if (HttpContext.Request["reference"].ToString() == null)
            {
                TempData["error"] = $"Transaction Invalid.";

                return RedirectToAction("TransactionHistory");
            }
            var tranxRef = HttpContext.Request["reference"].ToString();
            var transactionId = HttpContext.Request["transactionid"].ToString();
            if (tranxRef != null)
            {
                TransactionResponseModel response = await _paystack.Transactions.VerifyTransaction(tranxRef);
                //var id = 0;
                var id = int.Parse(transactionId);
                var transaction = await _transactions.GetTransaction(id);

                var userid = User.Identity.GetUserId();
                var clientedit = await _clientService.GetClientDetailsByUserId(userid);

                if (response.status == true)
                {


                    //clientedit.Units = transaction.Amount;

                    if (transaction == null)
                    {
                        TempData["warning"] = $"Transaction with Reference {tranxRef} was successful. But Unit was not updated. Please contact Help Desk.";
                        return RedirectToAction("TransactionDetails", new { id = transaction.TransactionId });
                    }
                    else if (!string.IsNullOrEmpty(transaction.TransactionReference))
                    {
                        TempData["warning"] = $"Transaction with Reference {tranxRef} was successful.";
                        return RedirectToAction("TransactionDetails", new { id = transaction.TransactionId });
                    }
                    else
                    {


                        transaction.Status = TransactionStatus.Approved;
                        transaction.DateApproved = DateTime.UtcNow.AddHours(1);
                        transaction.TransactionReference = tranxRef;
                        transaction.AmountPaid = transaction.Amount;
                        transaction.ApprovedBy = "Online";

                        await _transactions.UpdateTransactionStatus(transaction);

                        clientedit.Units += transaction.Amount;
                        await _clientService.UpdateClient(clientedit);


                        TempData["success"] = $"Transaction with Reference {tranxRef} was successful.";

                        string MessageBody = TempData["success"] + " Your xyzsms account has been credited. Balance is N" + clientedit.Units + ". Thanks for your patronage @ http://xyzsms.com.";
                        var emailMessage = string.Format("{0};??{1};??{2};??{3}", "Transaction Notification", "Transaction Notification", "Thanks " + clientedit.User.UserName, MessageBody);

                        await _email.SendEmailAsync(emailMessage, clientedit.User.Email, "Transaction Notification");
                        await _clientService.SendSms("xyzsms", MessageBody, clientedit.User.PhoneNumber);


                        return RedirectToAction("TransactionDetails", new { id = transaction.TransactionId });
                    }


                }
                else
                {

                    transaction.Status = TransactionStatus.Failed;
                    transaction.TransactionReference = tranxRef;
                    await _transactions.UpdateTransactionStatus(transaction);
                    TempData["error"] = $"Transaction with Reference {tranxRef} failed.";

                    return RedirectToAction("TransactionDetails", new { id = transaction.TransactionId });

                }

            }

            TempData["error"] = $"Transaction with Reference {tranxRef} failed.";

            return RedirectToAction("TransactionHistory");
        }
        public async Task<ActionResult> SenderByUser()
        {
            string userId = User.Identity.GetUserId();
            var client = await _clientService.GetClientDetailsByUserId(userId);
            ViewBag.name = client.Surname + " " + client.FirstName + " " + client.OtherNames;
            return View(await _clientService.GetAllSenderIdById(userId));
        }


        // GET: Adminpanel/XyzSenderIDs/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Adminpanel/XyzSenderIDs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string SenderId, string Message)
        {
            if (ModelState.IsValid)
            {
                string userId = User.Identity.GetUserId();
                var response = await _clientService.AddSender(userId, SenderId, Message);
                TempData["success"] = response;
                return RedirectToAction("SenderByUser");
            }
            TempData["error"] = "unable to process";
            return View(); 
        }


        //user profile
        public async Task<ActionResult> Details()
        {
            var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            return View(client);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}