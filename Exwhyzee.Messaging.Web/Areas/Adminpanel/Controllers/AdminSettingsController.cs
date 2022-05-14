using Hangfire;
using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AdminSettingsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private IAdminSettings _adminSettings = new AdminSettings();
        private IClientService _clientService = new ClientService();

        public AdminSettingsController()
        {
        }

        public AdminSettingsController(AdminSettings adminSettings)
        {
            _adminSettings = adminSettings;
        }

        // GET: Adminpanel/AdminSettings/Edit/5
        public async Task<ActionResult> EditSettings()
        {
            AdminSetting adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            if (adminSetting == null)
            {
                return HttpNotFound();
            }
            return View(adminSetting);
        }

        // POST: Adminpanel/AdminSettings/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditSettings([Bind(Include = "AdminSettingId,BlackListedWords,UnitPerNewMember,FlatUnitsPerSms,DefaultUserUnitReorderLevel,PricePerUnit,SendOrderApprovedNotification,SendLowOnUnitsNotification,SendRecievedRequestNotification,SendReminderToDebtor,SendAccountCreditedNotification,SendUserBirthdayWishes,PreventApiModification")] AdminSetting adminSetting)
        {
            if (ModelState.IsValid)
            {
                db.Entry(adminSetting).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["EditSettings"] = "Changes Updated.";
                return RedirectToAction("EditSettings");
            }
            return View(adminSetting);
        }

        public ActionResult ExecuteScheduleMessage()
        {
            RecurringJob.AddOrUpdate(() => ScheduleBdMessage(), Cron.Daily(1));

            return View("EditSettings");
        }

        public async Task ScheduleBdMessage()
        {
            DateTime currentDate = DateTime.UtcNow.AddHours(1);
            //get all Groups that allows birthday Messages
            var groups = db.Groups.Where(x => x.SendBirthDayMessages == true);

            foreach (var group in await groups.ToListAsync())
            {
                var contacts = db.Contacts.Where(x => x.GroupId == group.GroupId && !string.IsNullOrEmpty(x.PhoneNumber) && x.DateOfBirth.Value.Date == currentDate.Date).Select(x => x.PhoneNumber);

                foreach (var contect in await contacts.ToListAsync())
                {
                    string contactsToSend = string.Join(",", contacts.ToArray());

                    //get page count
                    int pageCount = SmsServices.CountPage(group.Message);

                    //Remove duplicate Numbers
                    List<string> numbers = new List<string>(SmsServices.RemoveDuplicates(contactsToSend));

                    //Format Numbers with International dail codes
                    List<string> fNumbers = new List<string>(SmsServices.FormatNumbers(numbers.ToList()));

                    //units needed per page
                    decimal units = SmsServices.UnitsPerPage(fNumbers.ToList());

                    //total units needed
                    decimal totalUnitsNeeded = pageCount * units;

                    var clientsUnits = await db.Clients.FirstOrDefaultAsync(x => x.UserId == group.UserId);
                    string recipients = string.Join(",", fNumbers.ToList());
                    //Check if Client's Unit is sufficient
                    if (totalUnitsNeeded > clientsUnits.Units)
                    {
                        //Save message to History
                        var message = new Message()
                        {
                            DeliveredDate = DateTime.UtcNow.AddHours(1),
                            MessageContent = group.Message,
                            Recipients = recipients,
                            Status = MessageStatus.Sent,
                            Response = "Error. Insufficient balance",
                            SenderId = group.SenderId,
                            UserId = group.UserId
                        };
                        db.Messages.Add(message);
                        await db.SaveChangesAsync();
                        // throw new Exception("Sending Message failed. You have insufficient unit balance. Your current balance is " + clientsUnits.Units + "units, while total units required is " + totalUnitsNeeded + ".");
                    }
                    else
                    {
                        var responseMessage = await _clientService.SendSms(group.SenderId, group.Message, recipients);

                        if (responseMessage.ToUpper().Contains("OK"))
                        {
                            //Save message to History
                            var message = new Message()
                            {
                                DeliveredDate = DateTime.UtcNow.AddHours(1),
                                MessageContent = group.Message,
                                Recipients = recipients,
                                Status = MessageStatus.Sent,
                                Response = "Sent",
                                SenderId = group.SenderId,
                                UserId = group.UserId
                            };

                            db.Messages.Add(message);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            //Save message to History
                            var message = new Message()
                            {
                                DeliveredDate = DateTime.UtcNow.AddHours(1),
                                MessageContent = group.Message,
                                Recipients = recipients,
                                Status = MessageStatus.Sent,
                                Response = responseMessage,
                                SenderId = group.SenderId,
                                UserId = group.UserId
                            };

                            db.Messages.Add(message);
                            await db.SaveChangesAsync();
                        }
                    }
                }
            }
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