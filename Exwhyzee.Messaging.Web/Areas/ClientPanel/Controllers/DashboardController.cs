using Microsoft.AspNetCore.Http;
using Hangfire;
using Exwhyzee.Messaging.Web.Controllers;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using Exwhyzee.Messaging.Core.ViewModels;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using X.PagedList;
using Exwhyzee.Messaging.Core.Paystack;
using Exwhyzee.Messaging.Core.Paystack.Models;
using static Exwhyzee.Messaging.Core.Services.GeneralServices;
using X.PagedList.Extensions;

namespace Exwhyzee.Messaging.Web.Areas.ClientPanel.Controllers
{
    [Authorize(Roles = "Client")]
    [Area("ClientPanel")]
    public class DashboardController : BaseController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        private IClientService _clientService = new ClientService();
        private ISendEmail _email = new SendEmail();
        private IZeptoMailService _zeptoMail = new ZeptoMailService();

        private IPayStackApi _paystack => new PayStackApi(AppConfig.PayStackSecretKey);
        private ITransactionService _transactions = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private IPaystackTransactionService _paystackTransactionService = new PaystackTransactionService();
        private IGeminiContactExtractorService _geminiExtractor = new GeminiContactExtractorService();
        private ITwoFactorService _twoFactorService = new TwoFactorService();
        private System.Random randomInteger = new System.Random();

        public DashboardController()
        {
        }
        [HttpGet]
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

            // counts
            ViewBag.ScheduleCount = lastSchedule.Count();
            var userId = User.Identity.GetUserId();
            ViewBag.TotalMessagesSent = await db.Messages.Where(x => x.UserId == userId).CountAsync();
            ViewBag.SenderIdCount = await db.XyzSenderIDs.Where(x => x.ClientId == userclient.ClientId).CountAsync();
            ViewBag.ApprovedTransactions = await db.Transactions.Where(x => x.ClientId == userclient.ClientId && x.Status == TransactionStatus.Approved).CountAsync();

            // Analytics: Top Sender IDs Breakdown
            var topSenderIds = await db.Messages
                .Where(x => x.UserId == userId && !string.IsNullOrEmpty(x.SenderId))
                .GroupBy(x => x.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            ViewBag.SenderLabels = topSenderIds.Select(s => s.SenderId).ToArray();
            ViewBag.SenderCounts = topSenderIds.Select(s => s.Count).ToArray();

            // Analytics: Delivery Status Distribution
            var statusSent = await db.Messages.CountAsync(x => x.UserId == userId && x.Status == MessageStatus.Sent);
            var statusPending = await db.Messages.CountAsync(x => x.UserId == userId && x.Status == MessageStatus.Pending);
            var statusFailed = await db.Messages.CountAsync(x => x.UserId == userId && x.Status == MessageStatus.Failed);

            ViewBag.StatusSent = (statusSent == 0 && statusPending == 0 && statusFailed == 0) ? 85 : statusSent;
            ViewBag.StatusPending = statusPending;
            ViewBag.StatusFailed = (statusSent == 0 && statusPending == 0 && statusFailed == 0) ? 5 : statusFailed;

            // Analytics: 7-Day Trend Labels
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.UtcNow.Date.AddDays(-6 + i))
                .ToList();
            ViewBag.TrendLabels = last7Days.Select(d => d.ToString("MMM dd")).ToArray();

            var modal = await db.ModalInfos.FirstOrDefaultAsync();
            ViewBag.Modal = modal;
            return View();
        }

        //
        // GET: /ClientPanel/Dashboard/Notifications
        public async Task<ActionResult> Notifications()
        {
            var userId = User.Identity.GetUserId();
            var userclient = await _clientService.GetClientDetailsByUserId(userId);

            // Fetch Real AppNotifications from DB
            var notifs = await db.AppNotifications
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.DateCreated)
                .Take(30)
                .ToListAsync();

            // If empty, auto-seed default welcome alerts
            if (!notifs.Any())
            {
                var welcomeNotif = new AppNotification
                {
                    UserId = userId,
                    Title = "Welcome to Exwhyzee Bulk SMS",
                    Message = "Your account is active with 20 complimentary test units. Start sending high-deliverability SMS!",
                    NotificationType = "Wallet",
                    ActionUrl = "/ClientPanel/Dashboard/compose",
                    IsRead = false,
                    DateCreated = DateTime.UtcNow
                };
                var senderNotif = new AppNotification
                {
                    UserId = userId,
                    Title = "Register Your Sender ID",
                    Message = "Submit alphanumeric sender IDs to send branded messages to your customers.",
                    NotificationType = "SenderId",
                    ActionUrl = "/ClientPanel/Dashboard/SenderByUser",
                    IsRead = false,
                    DateCreated = DateTime.UtcNow.AddMinutes(-5)
                };
                db.AppNotifications.AddRange(welcomeNotif, senderNotif);
                await db.SaveChangesAsync();

                notifs = new List<AppNotification> { welcomeNotif, senderNotif };
            }

            ViewBag.NotificationsList = notifs;
            ViewBag.UnreadCount = notifs.Count(x => !x.IsRead);

            var recentTransactions = await db.Transactions
                .Where(x => x.ClientId == userclient.ClientId)
                .OrderByDescending(x => x.DateCreated)
                .ThenByDescending(x => x.TransactionId)
                .Take(10)
                .ToListAsync();

            var recentSenderIds = await db.XyzSenderIDs
                .Where(x => x.ClientId == userclient.ClientId)
                .OrderByDescending(x => x.Id)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentTransactions = recentTransactions;
            ViewBag.RecentSenderIds = recentSenderIds;
            ViewBag.Client = userclient;

            return View();
        }

        //
        // POST: /ClientPanel/Dashboard/MarkNotificationRead
        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(int id)
        {
            var userId = User.Identity.GetUserId();
            var notif = await db.AppNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (notif != null)
            {
                notif.IsRead = true;
                notif.DateRead = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Notification not found" });
        }

        //
        // POST: /ClientPanel/Dashboard/MarkAllNotificationsRead
        [HttpPost]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var userId = User.Identity.GetUserId();
            var unread = await db.AppNotifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync();
            foreach (var item in unread)
            {
                item.IsRead = true;
                item.DateRead = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction("Notifications");
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

        // Helper to populate all necessary ViewBag items for Compose view
        private async Task PopulateComposeViewBagAsync(string userId, Client client)
        {
            // 1. Registered & Approved Sender IDs
            var userSenderIds = await _clientService.GetAllSenderIdById(userId);
            var approvedList = userSenderIds
                .Where(x => x.XYZ_status == "Approved" || x.XYZ_status == "Active")
                .Select(x => x.SenderId?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            ViewBag.ApprovedSenderIds = approvedList;
            var sidStatusList = userSenderIds.Select(x => new
            {
                SenderId = x.SenderId?.Trim() ?? "",
                Status = x.XYZ_status ?? "Pending Approval"
            }).ToList();
            ViewBag.SenderIdsStatusJson = System.Text.Json.JsonSerializer.Serialize(sidStatusList);

            // 2. Address Book Groups with Member Count
            var groups = await db.Groups
                .Where(x => x.Name != null && x.UserId == userId)
                .OrderBy(x => x.Name)
                .Select(g => new
                {
                    GroupId = g.GroupId,
                    Name = g.Name,
                    ContactCount = db.Contacts.Count(c => c.GroupId == g.GroupId && c.IsActive)
                }).ToListAsync();

            ViewBag.GroupList = groups;
            ViewBag.GroupId = new MultiSelectList(groups, "GroupId", "Name");

            // 3. User Wallet Balance & Gemini Configuration
            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            ViewBag.WalletBalance = client?.Units ?? 0;
            ViewBag.HasGeminiKey = !string.IsNullOrWhiteSpace(client?.GeminiApiKey);
            ViewBag.BaseUnitsPerSms = adminSetting?.FlatUnitsPerSms ?? 4.0m;

            // 4. Tariff Dial Rules for Real-Time Cost Estimator
            try
            {
                var dialRules = await db.DialCodes
                    .Include(d => d.PriceSetting)
                    .AsNoTracking()
                    .Where(d => d.PriceSetting != null)
                    .Select(d => new
                    {
                        Prefix = d.NumberPrefix,
                        DialCode = d.PriceSetting.InternationalDialCode ?? "234",
                        Rate = d.PriceSetting.UnitsPerSms
                    })
                    .ToListAsync();

                ViewBag.TariffRulesJson = System.Text.Json.JsonSerializer.Serialize(dialRules);
            }
            catch
            {
                ViewBag.TariffRulesJson = "[]";
            }
        }

        [HttpGet]
        public async Task<ActionResult> Compose()
        {
            string userId = User.Identity.GetUserId();
            var client = await _clientService.GetClientDetailsByUserId(userId);
            await PopulateComposeViewBagAsync(userId, client);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateSmsAi(string prompt, string tone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    return Json(new { success = false, message = "Please enter a description or prompt for the SMS." });
                }

                string userId = User.Identity.GetUserId();
                var client = await _clientService.GetClientDetailsByUserId(userId);
                string geminiKey = client?.GeminiApiKey;

                if (string.IsNullOrWhiteSpace(geminiKey))
                {
                    return Json(new { success = false, keyMissing = true, message = "Google Gemini API Key is not configured. Please add your key in API Settings." });
                }

                string toneDesc = string.IsNullOrWhiteSpace(tone) ? "Professional and Friendly" : tone;

                string systemPrompt = $@"You are an expert SMS copywriter. Write an effective, high-converting, crisp SMS broadcast message based on the user's request.
Tone: {toneDesc}
User Request: {prompt}

STRICT NON-NEGOTIABLE RULES:
1. Do NOT include ANY emojis, emoticons, or non-standard symbols. Use ONLY standard alphanumeric plain ASCII / GSM-7 text (letters, numbers, basic punctuation . , ! ? - / : ; @ &).
2. If the user asks for a specific length or page count (e.g. 1 page, 2 pages, etc.), adhere to it closely. Otherwise, write a punchy, impactful SMS copy.
3. Do NOT wrap the output in quotes, backticks, or write conversational explanations (e.g. 'Here is your SMS:'). Return ONLY the raw plain text of the SMS message itself.";

                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = systemPrompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 1000
                    }
                };

                string rawResponse = await _geminiExtractor.PostToGeminiWithFallbackAsync(payload, geminiKey);
                string generatedText = "";

                using var doc = System.Text.Json.JsonDocument.Parse(rawResponse);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    if (first.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        generatedText = parts[0].GetProperty("text").GetString()?.Trim() ?? "";
                    }
                }

                // Strip any rogue emojis or enclosing markdown / quotes
                generatedText = System.Text.RegularExpressions.Regex.Replace(generatedText, @"[^\u0000-\u007F]+", "");
                generatedText = generatedText.Trim('"', '\'', '`', ' ');

                return Json(new { success = true, text = generatedText });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> Compose(ComposeViewModel model, int[] GroupId, IFormFile file)
        {
            string userId = User.Identity.GetUserId();
            var client = await _clientService.GetClientDetailsByUserId(userId);

            try
            {
                // Multi-Format File Ingestion (.xlsx, .xls, .docx, .txt, .csv)
                if (file != null && file.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        byte[] fileBytes = ms.ToArray();
                        var extracted = await _geminiExtractor.ExtractFromFileAsync(fileBytes, file.FileName, client?.GeminiApiKey);
                        if (extracted != null && extracted.Any())
                        {
                            string numbers = string.Join(",", extracted.Select(c => c.Phone));
                            model.Recipients = (model.Recipients ?? "") + "," + numbers;
                        }
                    }
                    catch
                    {
                        // Fallback to basic text read
                        if (file.FileName.ToLower().Contains("txt"))
                        {
                            using var reader = new StreamReader(file.OpenReadStream());
                            string txtContent = await reader.ReadToEndAsync();
                            string numbers = txtContent.Replace("\r\n", ",").Replace("\n", ",").Replace(" ", ",");
                            model.Recipients = (model.Recipients ?? "") + "," + numbers;
                        }
                    }
                }

                if (GroupId != null && GroupId.Length > 0)
                {
                    string combined = "";
                    foreach (var item in GroupId)
                    {
                        var itemContacts = await db.Contacts.Where(x => x.GroupId == item && x.IsActive).Select(x => x.PhoneNumber).ToListAsync();
                        if (itemContacts.Any())
                        {
                            combined = combined + string.Join(",", itemContacts) + ",";
                        }
                    }
                    if (!string.IsNullOrEmpty(combined) && combined.EndsWith(","))
                    {
                        combined = combined.Remove(combined.Length - 1);
                    }

                    model.Recipients = (model.Recipients ?? "") + (string.IsNullOrEmpty(model.Recipients) ? "" : ",") + combined;
                }

                if (!string.IsNullOrEmpty(model.Recipients))
                {
                    model.Recipients = model.Recipients.Trim().TrimEnd(',');
                }

                if (ModelState.IsValid)
                {
                    if (string.IsNullOrWhiteSpace(model.Recipients))
                    {
                        await PopulateComposeViewBagAsync(userId, client);
                        ModelState.AddModelError("", "Message sending failed. No recipient was added or selected.");
                        return View(model);
                    }

                    // Clean and sanitize Sender ID (alphanumeric, max 11)
                    string rawSenderId = model.SenderId?.Trim() ?? "";
                    string cleanSenderId = System.Text.RegularExpressions.Regex.Replace(rawSenderId, @"[^a-zA-Z0-9]", "").ToUpper();
                    if (cleanSenderId.Length > 11)
                    {
                        cleanSenderId = cleanSenderId.Substring(0, 11);
                    }
                    model.SenderId = cleanSenderId;

                    if (string.IsNullOrEmpty(cleanSenderId))
                    {
                        await PopulateComposeViewBagAsync(userId, client);
                        ModelState.AddModelError("", "Please enter a valid alphanumeric Sender ID (max 11 characters, no spaces or symbols).");
                        return View(model);
                    }

                    // Auto-sync or verify Sender ID with Gateway
                    if (client != null)
                    {
                        var existingSid = await db.XyzSenderIDs.FirstOrDefaultAsync(x => x.SenderId == cleanSenderId && x.ClientId == client.ClientId);
                        if (existingSid == null)
                        {
                            // Auto-submit brand new Sender ID to KudiSMS in background
                            try
                            {
                                await _clientService.AddSender(userId, cleanSenderId, "Account verification and transactional alerts.");
                            }
                            catch { }
                        }
                        else if (existingSid.XYZ_status != "Approved" && existingSid.XYZ_status != "Active")
                        {
                            // Quick on-the-fly live verification check
                            try
                            {
                                var verifyRes = await _clientService.VerifySender(cleanSenderId);
                                if (verifyRes == "Approved")
                                {
                                    existingSid.XYZ_status = "Approved";
                                    existingSid.XYZ_msg = "Approved and active for SMS broadcasts.";
                                    await db.SaveChangesAsync();
                                }
                            }
                            catch { }
                        }
                    }

                    // Count pages
                    int pageCount = SmsServices.CountPage(model.Content);

                    // Deduplicate and format numbers
                    List<string> rawNumbers = SmsServices.RemoveDuplicates(model.Recipients).ToList();
                    List<string> fNumbers = SmsServices.FormatNumbers(rawNumbers).ToList();

                    if (!fNumbers.Any())
                    {
                        await PopulateComposeViewBagAsync(userId, client);
                        ModelState.AddModelError("", "Please enter at least one valid phone number.");
                        return View(model);
                    }

                    // Units needed per page and total
                    decimal units = SmsServices.UnitsPerPage(fNumbers);
                    decimal totalUnitsNeeded = pageCount * units;

                    // Check if Client's Unit balance is sufficient
                    if (client == null || totalUnitsNeeded > client.Units)
                    {
                        await PopulateComposeViewBagAsync(userId, client);
                        TempData["error"] = $"Insufficient unit balance. Your current balance is {client?.Units ?? 0} units, but {totalUnitsNeeded} units are required.";
                        return View(model);
                    }

                    // Parse Schedule Date cleanly without throwing FormatException
                    DateTime parsedScheduleDate = DateTime.Now.AddMinutes(30);
                    if (model.ScheduleDate != null && !string.IsNullOrWhiteSpace(model.ScheduleDate.ToString()))
                    {
                        string sDateStr = model.ScheduleDate.ToString().Trim();
                        string[] formats = new[] {
                            "dd/MM/yyyy HH:mm", "dd/MM/yyyy hh:mm tt", "dd/MM/yyyy h:mm tt", "dd/MM/yyyy HH:mm:ss",
                            "dd/MM/yyyy", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss",
                            "d/M/yyyy HH:mm", "d/M/yyyy h:mm tt", "MM/dd/yyyy HH:mm", "MM/dd/yyyy hh:mm tt",
                            "yyyy-MM-dd", "M/d/yyyy", "d/M/yyyy"
                        };

                        if (DateTime.TryParseExact(sDateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dExact))
                        {
                            parsedScheduleDate = dExact;
                        }
                        else if (DateTime.TryParse(sDateStr, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.None, out var dGb))
                        {
                            parsedScheduleDate = dGb;
                        }
                        else if (DateTime.TryParse(sDateStr, out var d1))
                        {
                            parsedScheduleDate = d1;
                        }
                    }

                    // Save message to History
                    Message message = new Message
                    {
                        DeliveredDate = DateTime.UtcNow,
                        MessageContent = model.Content,
                        Recipients = string.Join(",", fNumbers),
                        Response = "Pending",
                        SenderId = model.SenderId?.ToString() ?? "",
                        Scheduleddate = parsedScheduleDate,
                        UnitsUsed = 0,
                        UserId = userId
                    };

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

                    await _clientService.AddMessageToHistory(message);

                    if (model.SendOption == "SendNow")
                    {
                        var response = await _clientService.SendSmsById(message.MessageId, totalUnitsNeeded);
                        var sentMessage = await _clientService.GetMessage(message.MessageId);

                        if (response != null && response.status != null && response.status.ToLower().Contains("success"))
                        {
                            // Deduct units
                            client.Units = client.Units - totalUnitsNeeded;
                            await _clientService.UpdateClient(client);

                            // Asynchronous Low Unit Alert Trigger
                            try
                            {
                                var lowUnitService = new LowUnitAlertService();
                                _ = Task.Run(() => lowUnitService.CheckAndTriggerLowUnitAlertAsync(userId, client.Units));
                            }
                            catch { }

                            if (sentMessage != null)
                            {
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

                            TempData["success"] = $"Message sent successfully to {fNumbers.Count} recipient(s). Total units used: {totalUnitsNeeded}.";
                            return RedirectToAction("Compose");
                        }
                        else
                        {
                            string errMessage = response?.msg ?? "Error from SMS Gateway.";
                            if (response?.error_code == "106")
                            {
                                errMessage = "The sender ID used does not exist or has not been approved.";
                            }

                            if (sentMessage != null)
                            {
                                sentMessage.Response = response?.msg;
                                sentMessage.Response_status = response?.status ?? "Failed";
                                sentMessage.Response_error_code = response?.error_code;
                                sentMessage.Response_cost = response?.cost;
                                sentMessage.Response_data = response?.data;
                                sentMessage.Response_msg = response?.msg;
                                sentMessage.Response_length = response?.length ?? 0;
                                sentMessage.Response_page = response?.page ?? 0;
                                sentMessage.Response_balance = response?.balance;
                                sentMessage.Response_BalanceResponse = response?.BalanceResponse;
                                await _clientService.UpdateMessageStatus(sentMessage);
                            }

                            TempData["error"] = errMessage;
                            return RedirectToAction("Compose");
                        }
                    }
                    else if (model.SendOption == "SendLater")
                    {
                        TempData["success"] = $"Broadcast to {fNumbers.Count} recipient(s) has been successfully scheduled for {parsedScheduleDate:dd MMM yyyy, hh:mm tt}!";
                        return RedirectToAction("ScheduledMessages");
                    }
                    else if (model.SendOption == "SaveDraft")
                    {
                        TempData["success"] = "Message has been saved as Draft successfully.";
                        return RedirectToAction("DraftMessages");
                    }
                }

                await PopulateComposeViewBagAsync(userId, client);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error sending message: " + ex.Message;
                return RedirectToAction("Compose");
            }
        }

        // ========================================================
        // SCHEDULED MESSAGES QUEUE & MANAGEMENT
        // ========================================================

        // GET: ClientPanel/Dashboard/ScheduledMessages
        public async Task<ActionResult> ScheduledMessages(string searchString, string currentFilter, int? page)
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
            var query = db.Messages.Where(x => x.UserId == user.Id && x.Status == MessageStatus.Scheduled);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.SenderId.Contains(searchString) || s.MessageContent.Contains(searchString) || s.Recipients.Contains(searchString));
            }

            var list = await query.OrderBy(x => x.Scheduleddate).ToListAsync();
            int pageSize = 15;
            int pageNumber = page ?? 1;

            return View(list.ToPagedList(pageNumber, pageSize));
        }

        // GET: ClientPanel/Dashboard/EditScheduledMessage/5
        public async Task<ActionResult> EditScheduledMessage(int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var message = await db.Messages.FirstOrDefaultAsync(m => m.MessageId == id && m.UserId == user.Id && m.Status == MessageStatus.Scheduled);
            if (message == null)
            {
                TempData["error"] = "Scheduled message not found or is no longer in scheduled queue.";
                return RedirectToAction("ScheduledMessages");
            }

            var userSenderIds = await _clientService.GetAllSenderIdById(user.Id);
            ViewBag.ApprovedSenderIds = userSenderIds.Where(x => x.XYZ_status == "Approved" || x.XYZ_status == "Active").Select(x => x.SenderId?.Trim()).Distinct().ToList();

            return View(message);
        }

        // POST: ClientPanel/Dashboard/EditScheduledMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditScheduledMessage(int id, string senderId, string recipients, string messageContent, string scheduleDate)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var message = await db.Messages.FirstOrDefaultAsync(m => m.MessageId == id && m.UserId == user.Id && m.Status == MessageStatus.Scheduled);
            if (message == null)
            {
                TempData["error"] = "Scheduled message not found or cannot be edited.";
                return RedirectToAction("ScheduledMessages");
            }

            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(recipients) || string.IsNullOrWhiteSpace(messageContent))
            {
                TempData["error"] = "Please fill in all required fields.";
                return RedirectToAction("EditScheduledMessage", new { id });
            }

            DateTime parsedScheduleDate = DateTime.Now.AddMinutes(30);
            if (!string.IsNullOrWhiteSpace(scheduleDate))
            {
                if (DateTime.TryParse(scheduleDate, out var dt))
                {
                    parsedScheduleDate = dt;
                }
                else if (DateTime.TryParseExact(scheduleDate, new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy hh:mm", "yyyy-MM-ddTHH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtExact))
                {
                    parsedScheduleDate = dtExact;
                }
            }

            message.SenderId = senderId.Trim().ToUpper();
            message.Recipients = recipients.Trim();
            message.MessageContent = messageContent.Trim();
            message.Scheduleddate = parsedScheduleDate;

            db.Entry(message).State = EntityState.Modified;
            await db.SaveChangesAsync();

            TempData["success"] = "Scheduled broadcast updated successfully!";
            return RedirectToAction("ScheduledMessages");
        }

        // POST: ClientPanel/Dashboard/DeleteScheduledMessage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteScheduledMessage(int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var message = await db.Messages.FirstOrDefaultAsync(m => m.MessageId == id && m.UserId == user.Id);
            if (message != null)
            {
                db.Messages.Remove(message);
                await db.SaveChangesAsync();
                TempData["success"] = "Scheduled broadcast has been cancelled and deleted.";
            }
            return RedirectToAction("ScheduledMessages");
        }

        // POST: ClientPanel/Dashboard/SendScheduledNow/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendScheduledNow(int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var message = await db.Messages.FirstOrDefaultAsync(m => m.MessageId == id && m.UserId == user.Id && m.Status == MessageStatus.Scheduled);
            if (message == null)
            {
                TempData["error"] = "Scheduled message not found or is no longer in scheduled queue.";
                return RedirectToAction("ScheduledMessages");
            }

            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            var rawNumbers = SmsServices.RemoveDuplicates(message.Recipients ?? "");
            var formattedNumbers = SmsServices.FormatNumbers(rawNumbers);

            if (!formattedNumbers.Any())
            {
                TempData["error"] = "No valid recipients found in this message.";
                return RedirectToAction("ScheduledMessages");
            }

            int pageCount = SmsServices.CountPage(message.MessageContent ?? "");
            decimal unitsRate = SmsServices.UnitsPerPage(formattedNumbers);
            decimal totalUnitsNeeded = pageCount * unitsRate;

            if (client.Units < totalUnitsNeeded)
            {
                TempData["error"] = $"Insufficient wallet units ({client.Units:N2} available vs {totalUnitsNeeded:N2} required). Please top up.";
                return RedirectToAction("ScheduledMessages");
            }

            var response = await _clientService.SendSmsById(message.MessageId, totalUnitsNeeded);
            if (response != null && response.status.ToLower().Contains("success"))
            {
                client.Units -= totalUnitsNeeded;
                await _clientService.UpdateClient(client);

                message.UnitsUsed = totalUnitsNeeded;
                message.Status = MessageStatus.Sent;
                message.DeliveredDate = DateTime.UtcNow;
                message.Response = response.msg;
                message.Response_status = response.status;
                message.Response_cost = response.cost;
                message.Response_msg = response.msg;
                message.Response_page = response.page;
                message.Response_error_code = response.error_code;
                await _clientService.UpdateMessageStatus(message);

                TempData["success"] = $"Scheduled broadcast dispatched immediately to {formattedNumbers.Count} recipient(s)! ({totalUnitsNeeded:N2} units used).";
                return RedirectToAction("MessageHistory");
            }
            else
            {
                TempData["error"] = $"Dispatch error: {response?.msg ?? "Gateway rejected broadcast."}";
                return RedirectToAction("ScheduledMessages");
            }
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
        public async Task<ActionResult> SendDraft(ComposeViewModel model, int[] GroupId, IFormFile file, int? id)
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
            if (file != null && file.Length > 0)
            {
                string directory = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "Uploads/Contacts/");
                int genNumber = randomInteger.Next(1000000000);
                string line;
                if (file.FileName.ToLower().Contains("txt"))
                {
                    var fileName = Path.GetFileName(file.FileName);
                    using (var stream = new System.IO.FileStream(Path.Combine(directory, genNumber + fileName), System.IO.FileMode.Create)) { file.CopyTo(stream); }
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

        // GET: ClientPanel/Dashboard/MessageHistory
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
            var query = db.Messages.Where(x => x.UserId == user.Id && x.Status != MessageStatus.Draft);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.SenderId.Contains(searchString) || s.Recipients.Contains(searchString) || s.MessageContent.Contains(searchString));
            }

            var allUserMessages = await db.Messages.Where(x => x.UserId == user.Id && x.Status != MessageStatus.Draft).ToListAsync();
            ViewBag.TotalMessages = allUserMessages.Count;
            ViewBag.TotalUnitsUsed = allUserMessages.Sum(x => x.UnitsUsed);
            ViewBag.DeliveredCount = allUserMessages.Count(x => x.Status == MessageStatus.Sent);

            var msgList = await query.OrderByDescending(x => x.DeliveredDate ?? x.Scheduleddate ?? DateTime.MinValue).ToListAsync();

            int pageSize = 15;
            int pageNumber = page ?? 1;

            return View(msgList.ToPagedList(pageNumber, pageSize));
        }

        // GET: ClientPanel/Dashboard/GetMessageDetailsAjax/5
        [HttpGet]
        public async Task<IActionResult> GetMessageDetailsAjax(int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var msg = await db.Messages.FirstOrDefaultAsync(m => m.MessageId == id && m.UserId == user.Id);
            if (msg == null)
            {
                return Json(new { success = false, message = "Message not found." });
            }

            var rawRecipients = (msg.Recipients ?? "").Split(new[] { ',', ' ', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var uniqueRecipients = rawRecipients.Distinct().ToList();
            int pageCount = SmsServices.CountPage(msg.MessageContent ?? "");

            return Json(new
            {
                success = true,
                messageId = msg.MessageId,
                senderId = msg.SenderId,
                messageContent = msg.MessageContent,
                recipients = string.Join(", ", uniqueRecipients),
                recipientCount = uniqueRecipients.Count,
                unitsUsed = msg.UnitsUsed,
                status = msg.Status.ToString(),
                deliveredDate = msg.DeliveredDate.HasValue ? msg.DeliveredDate.Value.ToString("dd MMM yyyy, hh:mm tt") : "Pending",
                scheduledDate = msg.Scheduleddate.HasValue ? msg.Scheduleddate.Value.ToString("dd MMM yyyy, hh:mm tt") : "None",
                responseMsg = msg.Response_msg ?? msg.Response ?? "Dispatched",
                responseStatus = msg.Response_status ?? "Success",
                responseErrorCode = msg.Response_error_code ?? "0",
                responseCost = msg.Response_cost ?? "0",
                pageCount = pageCount,
                charCount = (msg.MessageContent ?? "").Length
            });
        }

        // GET: ClientPanel/Dashboard/DraftMessages
        public async Task<ActionResult> DraftMessages()
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var drafts = await db.Messages
                .Where(x => x.UserId == user.Id && x.Status == MessageStatus.Draft)
                .OrderByDescending(x => x.MessageId)
                .ToListAsync();

            return View(drafts);
        }

        // POST: ClientPanel/Dashboard/DeleteDraft/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteDraft(int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var draft = await db.Messages.FirstOrDefaultAsync(m => m.MessageId == id && m.UserId == user.Id && m.Status == MessageStatus.Draft);
            if (draft != null)
            {
                db.Messages.Remove(draft);
                await db.SaveChangesAsync();
                TempData["success"] = "Draft message deleted successfully.";
            }
            return RedirectToAction("DraftMessages");
        }

        // GET: ClientPanel/Dashboard/TransactionHistory
        public async Task<ActionResult> TransactionHistory(string searchString, string currentFilter, int? page)
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
            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            
            var approvedList = await db.Transactions.AsNoTracking()
                .Where(x => x.ClientId == client.ClientId && x.Status == TransactionStatus.Approved)
                .Select(t => new { AmountPaid = t.AmountPaid ?? t.Amount, Units = t.Units })
                .ToListAsync();

            ViewBag.TotalTransactions = approvedList.Count;
            ViewBag.TotalAmountPaid = approvedList.Sum(t => t.AmountPaid);
            ViewBag.TotalUnitsCredited = approvedList.Sum(t => t.Units);
            ViewBag.WalletBalance = client.Units;

            var query = db.Transactions.AsNoTracking().Where(x => x.ClientId == client.ClientId);

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(t => t.TransactionId.ToString().Contains(searchString) || (t.Note != null && t.Note.Contains(searchString)) || (t.TransactionReference != null && t.TransactionReference.Contains(searchString)));
            }

            int pageSize = 20;
            int pageNumber = page ?? 1;
            var list = query.OrderByDescending(x => x.DateCreated).ThenByDescending(x => x.TransactionId).ToPagedList(pageNumber, pageSize);

            return View(list);
        }

        // GET: ClientPanel/Dashboard/GetTransactionDetailsAjax/5
        [HttpGet]
        public async Task<IActionResult> GetTransactionDetailsAjax(int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            var trans = await db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id && t.ClientId == client.ClientId);
            if (trans == null)
            {
                return Json(new { success = false, message = "Transaction not found." });
            }

            return Json(new
            {
                success = true,
                transactionId = trans.TransactionId,
                units = trans.Units,
                amount = trans.Amount,
                amountPaid = trans.AmountPaid ?? trans.Amount,
                transactionType = trans.TransactionType.ToString(),
                status = trans.Status.ToString(),
                dateCreated = trans.DateCreated.ToString("dd MMM yyyy, hh:mm tt"),
                dateApproved = trans.DateApproved.HasValue ? trans.DateApproved.Value.ToString("dd MMM yyyy, hh:mm tt") : "Pending",
                description = trans.Note ?? "SMS Unit Purchase",
                reference = trans.TransactionReference ?? "N/A"
            });
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
                return NotFound();
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

        // GET: ClientPanel/Dashboard/NetworkPricing
        public async Task<ActionResult> NetworkPricing()
        {
            var priceSettings = await db.PriceSettings.Include(p => p.DialCodes).OrderBy(p => p.Country).ThenBy(p => p.NetworkProvider).ToListAsync();
            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            ViewBag.AdminSetting = adminSetting;
            ViewBag.PricePerUnit = adminSetting?.PricePerUnit ?? 2.0m;
            ViewBag.FlatUnits = adminSetting?.FlatUnitsPerSms ?? 2.0m;

            return View(priceSettings);
        }

        // GET: ClientPanel/Dashboard/BuyUnit
        public async Task<ActionResult> BuyUnit()
        {
            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            ViewBag.PricePerUnit = adminSetting?.PricePerUnit ?? 2.0m;
            ViewBag.PaystackPublicKey = AppConfig.PayStackPublicKey;

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            ViewBag.Client = client;
            ViewBag.UserEmail = user.Email;

            return View();
        }

        // Helper to calculate Paystack transaction charges (1.5% below 1500, 1.5% + 100 capped at 2000 above 1500)
        private decimal CalculatePaystackCharge(decimal amount)
        {
            if (amount <= 0) return 0;
            decimal fee;
            if (amount < 1500m)
            {
                // 1.5% charge
                fee = (amount * 0.015m) / 0.985m;
            }
            else
            {
                // 1.5% + 100 naira, capped at 2000 naira
                fee = ((amount + 100m) * 0.015m + 100m) / 0.985m;
                if (fee > 2000m)
                {
                    fee = 2000m;
                }
            }
            return Math.Round(fee, 2);
        }

        // POST: ClientPanel/Dashboard/InitializePaystackInline
        [HttpPost]
        public async Task<IActionResult> InitializePaystackInline(decimal units)
        {
            if (units <= 0)
            {
                return Json(new { success = false, message = "Please enter a valid unit quantity greater than zero." });
            }

            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            decimal pricePerUnit = adminSetting?.PricePerUnit ?? 2.0m;
            decimal subtotal = units * pricePerUnit;
            decimal charges = CalculatePaystackCharge(subtotal);
            decimal totalPayable = subtotal + charges;

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetailsByUserId(user.Id);

            Transaction transaction = new Transaction
            {
                Units = units,
                Amount = subtotal,
                AmountPaid = totalPayable,
                ClientId = client.ClientId,
                UserId = user.Id,
                TransactionType = TransactionType.OnlinePayment,
                PaymentSource = "Paystack",
                IsPaystackOutflow = true,
                IsAdminTransferred = false,
                Status = TransactionStatus.Pending,
                DateCreated = DateTime.UtcNow.AddHours(1),
                Note = $"Paystack Top-Up of {units:N2} units. SMS Cost: NGN {subtotal:N2}, Charges: NGN {charges:N2}, Total: NGN {totalPayable:N2}"
            };

            await _transactions.AddTransaction(transaction);

            int amountInKobo = (int)Math.Round(totalPayable * 100);

            return Json(new
            {
                success = true,
                transactionId = transaction.TransactionId,
                subtotal = subtotal,
                charges = charges,
                amount = totalPayable,
                amountInKobo = amountInKobo,
                units = units,
                pricePerUnit = pricePerUnit,
                email = user.Email,
                firstName = client.FirstName ?? user.UserName,
                surname = client.Surname ?? "",
                publicKey = AppConfig.PayStackPublicKey
            });
        }

        // POST: ClientPanel/Dashboard/VerifyPaystackInline
        [HttpPost]
        public async Task<IActionResult> VerifyPaystackInline(string reference, int transactionId)
        {
            if (string.IsNullOrWhiteSpace(reference) || transactionId <= 0)
            {
                return Json(new { success = false, message = "Invalid transaction parameters." });
            }

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetailsByUserId(user.Id);
            var transaction = await _transactions.GetTransaction(transactionId);

            if (transaction == null || transaction.ClientId != client.ClientId)
            {
                return Json(new { success = false, message = "Transaction record not found." });
            }

            if (transaction.Status == TransactionStatus.Approved)
            {
                return Json(new
                {
                    success = true,
                    message = "Transaction has already been credited.",
                    newBalance = client.Units,
                    units = transaction.Units,
                    amount = transaction.Amount
                });
            }

            try
            {
                TransactionResponseModel response = await _paystack.Transactions.VerifyTransaction(reference);

                if (response != null && response.status == true)
                {
                    // Approve transaction
                    transaction.Status = TransactionStatus.Approved;
                    transaction.DateApproved = DateTime.UtcNow.AddHours(1);
                    transaction.TransactionReference = reference;
                    transaction.AmountPaid = transaction.Amount;
                    transaction.ApprovedBy = "Paystack Inline";
                    transaction.IsPaystackOutflow = true;
                    transaction.GatewayResponse = response.message ?? "Successful";

                    // Credit Client Units
                    client.Units += transaction.Units;
                    await _clientService.UpdateClient(client);
                    await _transactions.UpdateTransactionStatus(transaction);

                    // Send in-app notification
                    var notifService = HttpContext.RequestServices.GetService<INotificationService>();
                    if (notifService != null)
                    {
                        await notifService.SendNotificationAsync(
                            user.Id,
                            "Wallet Credited via Paystack",
                            $"Your wallet was credited with {transaction.Units:N2} units (NGN {transaction.Amount:N2}). New balance: {client.Units:N2} units.",
                            "TOPUP_SUCCESS",
                            "/ClientPanel/Dashboard/TransactionHistory");
                    }

                    return Json(new
                    {
                        success = true,
                        message = $"Congratulations! Your wallet has been credited with {transaction.Units:N2} SMS units!",
                        newBalance = client.Units,
                        units = transaction.Units,
                        amount = transaction.Amount,
                        reference = reference
                    });
                }
                else
                {
                    transaction.GatewayResponse = response?.message ?? "Verification failed";
                    await _transactions.UpdateTransactionStatus(transaction);
                    return Json(new { success = false, message = response?.message ?? "Payment verification failed with Paystack." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Verification error: {ex.Message}" });
            }
        }

        // POST: ClientPanel/Dashboard/BuyUnit
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
                return NotFound();
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
                return NotFound();
            }

            int amountInKobo = (int)transaction.Amount * 100;
            var callbackUrl = Url.Action("Complete", "Dashboard", new { transactionid = id, area = "ClientPanel" }, protocol: Request.Scheme);

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
              if (HttpContext.Request.Query["reference"].ToString() == null)
            {
                TempData["error"] = $"Transaction Invalid.";

                return RedirectToAction("TransactionHistory");
            }
            var tranxRef = HttpContext.Request.Query["reference"].ToString();
            var transactionId = HttpContext.Request.Query["transactionid"].ToString();
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

                        decimal unitsToAdd = transaction.Units > 0 ? transaction.Units : transaction.Amount;
                        clientedit.Units += unitsToAdd;
                        await _clientService.UpdateClient(clientedit);

                        TempData["success"] = $"Transaction with Reference {tranxRef} was successful.";

                        string messageBody = $"Your XYZSMS account has been credited with {unitsToAdd:N0} units (₦{transaction.Amount:N2}). New balance: {clientedit.Units:N2} units. Thank you for your patronage!";

                        // Send rich HTML Receipt via ZeptoMail (automatically logged in EmailLogs)
                        try
                        {
                            await _zeptoMail.SendPaymentSuccessReceiptAsync(
                                recipientEmail: clientedit.User.Email,
                                username: clientedit.User.UserName ?? clientedit.FirstName,
                                unitsPurchased: unitsToAdd,
                                totalNewBalance: (decimal)clientedit.Units,
                                amountPaid: transaction.Amount,
                                transactionRef: tranxRef,
                                paymentMethod: "Online (Paystack)"
                            );
                        }
                        catch { }

                        try
                        {
                            await _clientService.SendSms("XYZSMS", messageBody, clientedit.User.PhoneNumber);
                        }
                        catch { }

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

        // POST: ClientPanel/Dashboard/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string SenderId, string Message)
        {
            if (string.IsNullOrWhiteSpace(SenderId))
            {
                TempData["error"] = "Please enter a valid alphanumeric Sender ID.";
                return RedirectToAction("SenderByUser");
            }

            SenderId = SenderId.Trim().ToUpper();
            if (SenderId.Length > 11)
            {
                TempData["error"] = "Sender ID cannot exceed 11 characters.";
                return RedirectToAction("SenderByUser");
            }

            string userId = User.Identity.GetUserId();
            var response = await _clientService.AddSender(userId, SenderId, Message ?? "Account verification and transactional alerts.");
            
            // Add In-App Notification
            try
            {
                var notif = new AppNotification
                {
                    UserId = userId,
                    Title = $"Sender ID Submitted: [{SenderId}]",
                    Message = $"Your request for Sender ID [{SenderId}] has been submitted to the gateway for operator approval.",
                    NotificationType = "SenderId",
                    ActionUrl = "/ClientPanel/Dashboard/SenderByUser",
                    IsRead = false,
                    DateCreated = DateTime.UtcNow
                };
                db.AppNotifications.Add(notif);
                await db.SaveChangesAsync();
            }
            catch { }

            TempData["success"] = response;
            return RedirectToAction("SenderByUser");
        }

        [HttpPost]
        public async Task<ActionResult> VerifySenderId(string senderId)
        {
            try
            {
                var response = await _clientService.VerifySender(senderId);
                TempData["success"] = $"Status for [{senderId}]: {response}";
            }
            catch (Exception ex)
            {
                TempData["error"] = "Unable to verify Sender ID at this time.";
            }
            return RedirectToAction("SenderByUser");
        }

        [HttpPost]
        public async Task<IActionResult> CheckSenderIdAjax(string senderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(senderId))
                {
                    return Json(new { success = false, message = "Invalid Sender ID" });
                }

                var status = await _clientService.VerifySender(senderId);
                return Json(new { success = true, senderId = senderId, status = status });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResubmitSenderAjax(string senderId, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(senderId))
                {
                    return Json(new { success = false, message = "Invalid Sender ID" });
                }

                string userId = User.Identity.GetUserId();
                var result = await _clientService.AddSender(userId, senderId.Trim().ToUpper(), message ?? "Account verification and transactional alerts.");
                return Json(new { success = true, message = result, senderId = senderId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSenderAjax(string senderId)
        {
            try
            {
                string userId = User.Identity.GetUserId();
                var client = await db.Clients.FirstOrDefaultAsync(x => x.UserId == userId);
                var sid = await db.XyzSenderIDs.FirstOrDefaultAsync(x => x.SenderId == senderId && x.ClientId == client.ClientId);
                if (sid != null)
                {
                    db.XyzSenderIDs.Remove(sid);
                    await db.SaveChangesAsync();
                    return Json(new { success = true, senderId = senderId });
                }
                return Json(new { success = false, message = "Sender ID not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        //user profile
        public async Task<ActionResult> Details()
        {
            var client = await _clientService.GetClientDetailsByUserId(User.Identity.GetUserId());
            return View(client);
        }

        #region TWO-FACTOR SECURITY SETTINGS

        [HttpGet]
        public async Task<IActionResult> TwoFactor()
        {
            var userId = UserManager.GetUserId(User);
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null) return RedirectToAction("Index");

            ViewBag.TwoFactorEnabled = user.TwoFactorEnabled;
            ViewBag.ActiveMethod = user.PreferredTwoFactorMethod;
            ViewBag.PhoneNumber = user.PhoneNumber;
            ViewBag.Email = user.Email;

            // Generate secret key for TOTP setup
            string secretKey = string.IsNullOrEmpty(user.TwoFactorSecretKey) ? _twoFactorService.GenerateSecretKey() : user.TwoFactorSecretKey;
            ViewBag.SecretKey = secretKey;

            string qrUri = _twoFactorService.GenerateQrCodeUri(user.UserName, secretKey);
            ViewBag.QrImageUrl = _twoFactorService.GenerateQrCodeImageUrl(qrUri);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactor(TwoFactorMethod method, string secretKey, string confirmationCode)
        {
            var userId = UserManager.GetUserId(User);
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null) return RedirectToAction("Index");

            if (method == TwoFactorMethod.GoogleAuth || method == TwoFactorMethod.MicrosoftAuth)
            {
                if (string.IsNullOrEmpty(confirmationCode) || !_twoFactorService.ValidateTotpCode(secretKey, confirmationCode))
                {
                    TempData["error"] = "Invalid 6-digit confirmation code from your Authenticator app. Please scan the QR Code Barcode and try again.";
                    return RedirectToAction("TwoFactor");
                }
                user.TwoFactorSecretKey = secretKey;
            }
            else if (method == TwoFactorMethod.SmsOtp)
            {
                if (string.IsNullOrEmpty(user.PhoneNumber))
                {
                    TempData["error"] = "Please update your phone number in Profile before enabling SMS OTP.";
                    return RedirectToAction("TwoFactor");
                }
            }
            else if (method == TwoFactorMethod.EmailOtp)
            {
                if (string.IsNullOrEmpty(user.Email))
                {
                    TempData["error"] = "Email address missing. Cannot enable Email OTP.";
                    return RedirectToAction("TwoFactor");
                }
            }

            user.TwoFactorEnabled = true;
            user.PreferredTwoFactorMethod = method;

            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u != null)
            {
                u.TwoFactorEnabled = true;
                u.PreferredTwoFactorMethod = method;
                u.TwoFactorSecretKey = user.TwoFactorSecretKey;
            }

            await db.SaveChangesAsync();
            TempData["success"] = $"Two-Factor Authentication ({method}) has been successfully enabled for your account!";
            return RedirectToAction("TwoFactor");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var userId = UserManager.GetUserId(User);
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null) return RedirectToAction("Index");

            user.TwoFactorEnabled = false;
            user.PreferredTwoFactorMethod = TwoFactorMethod.None;
            user.TwoFactorSecretKey = null;
            user.LastOtpCode = null;

            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u != null)
            {
                u.TwoFactorEnabled = false;
                u.PreferredTwoFactorMethod = TwoFactorMethod.None;
                u.TwoFactorSecretKey = null;
                u.LastOtpCode = null;
            }

            await db.SaveChangesAsync();
            TempData["success"] = "Two-Factor Authentication (2FA) has been disabled.";
            return RedirectToAction("TwoFactor");
        }

        #endregion

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