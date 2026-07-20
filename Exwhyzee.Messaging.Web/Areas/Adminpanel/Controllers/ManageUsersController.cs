using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Exwhyzee.Messaging.Core.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize]
    [Area("Adminpanel")]
    public class ManageUsersController : Controller
    {
        private ApplicationDbContext db;
        private UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> UserManager;
        private RoleManager<IdentityRole> RoleManager;
        private IClientService _clientService;

        public ManageUsersController(ApplicationDbContext _db, UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> _userManager, RoleManager<IdentityRole> _roleManager, IClientService clientService)
        {
            db = _db;
            UserManager = _userManager;
            RoleManager = _roleManager;
            _clientService = clientService;
        }

        // GET: Adminpanel/ManageUsers
        public ActionResult Index()
        {

            var users = UserManager.Users.ToList();

            ViewBag.Users = users;
            ViewBag.Roles = RoleManager.Roles.ToList();
            return View();

        }

        public ActionResult UserList()
        {

            var users = UserManager.Users.ToList();

            ViewBag.Users = users;
            return View();

        }

        public async Task<ActionResult> EditUser(string id)
        {
            var user = await UserManager.FindByIdAsync(id);
            return View(user);
        }
        [HttpPost]
        public async Task<ActionResult> EditUser(string id, string Email, bool EmailConfirmed,
            string PhoneNumber, bool PhoneNumberConfirmed, bool LockoutEnabled, DateTime? LockoutEnd, DateTime DateOfBirth)
        {
            try
            {


                var user = await UserManager.FindByIdAsync(id);
                user.Email = Email;
                user.EmailConfirmed = EmailConfirmed;
                user.PhoneNumber = PhoneNumber;
                user.PhoneNumberConfirmed = PhoneNumberConfirmed;
                user.LockoutEnabled = LockoutEnabled;
                if (!string.IsNullOrEmpty(LockoutEnd.ToString()))
                {
                    user.LockoutEnd = LockoutEnd;
                }

                user.DateOfBirth = DateOfBirth;
                // await UserManager.UpdateAsync(user);

                //update email
                var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                u.Email = Email;
                u.EmailConfirmed = EmailConfirmed;
                u.PhoneNumber = PhoneNumber;
                u.PhoneNumberConfirmed = PhoneNumberConfirmed;
                u.LockoutEnabled = LockoutEnabled;
                if (!string.IsNullOrEmpty(LockoutEnd.ToString()))
                {
                    u.LockoutEnd = LockoutEnd;
                }

                u.DateOfBirth = DateOfBirth;
                db.Entry(u).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return RedirectToAction("ClientDetails", new { id = id });
            }
            catch (Exception c)
            {
            }
            return View();
        }
        [HttpPost]
        public async Task<ActionResult> UserToRole(string rolename, string userId, bool? ischecked)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (ischecked.HasValue && ischecked.Value)
            {
                await UserManager.AddToRoleAsync(user, rolename);
            }
            else
            {
                await UserManager.RemoveFromRoleAsync(user, rolename);
            }

            return RedirectToAction("Index");
        }

        #region Client Details

        //complete registration
        public async Task<ActionResult> CompleteClientRegistration(string id)
        {

            var settings = db.AdminSettings.FirstOrDefault();
            var clientDetails = await _clientService.GetClientDetailsByUserId(id);
            if (clientDetails == null)
            {
                Client newClient = new Client();

                newClient.UserId = id;
                newClient.Units = settings.UnitPerNewMember;
                db.Clients.Add(newClient);
                await db.SaveChangesAsync();
                TempData["success"] = "User Registration Completed";
                return RedirectToAction("ClientDetails", new { id = newClient.UserId });
            }
            TempData["error"] = "User Registration Not Completed";
            return RedirectToAction("ClientDetails", new { id = id });
        }

        public async Task<ActionResult> ClientDetails(string id)
        {
            var clientDetails = await _clientService.GetClientDetailsByUserId(id);
            if (clientDetails == null)
            {
                TempData["error"] = "User Registration Not Completed";
                return RedirectToAction("UserList");
            }
            else
            {
                var user = await UserManager.FindByIdAsync(id);
                ViewBag.Details = user;
                return View(clientDetails);
            }

        }

        [HttpPost]
        public async Task<ActionResult> ClientDetails(Client model)
        {
            await _clientService.UpdateClient(model);
            TempData["Success"] = "Client Update was successful.";
            return RedirectToAction("Index");
        }

        #endregion Client Details

        #region Units

        public async Task<ActionResult> AddUnit(int id)
        {
            var client = await _clientService.GetClientDetails(id);

            if (client == null)
            {
                return NotFound();
            }
            ViewBag.Client = client;
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> AddUnit(AddUnitViewModel model, int id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var client = await _clientService.GetClientDetails(id);
            //var clientUsername = await UserManager.FindByIdAsync(client.UserId);
            if (ModelState.IsValid)
            {
                //
                //New transaction
                Transaction transaction = new Transaction();
                transaction.Units = model.Units;
                transaction.AmountPaid = model.AmountPaid;
                transaction.ClientId = id;
                transaction.Amount = await _clientService.GetAmount(model.Units);
                transaction.DateApproved = DateTime.UtcNow;
                transaction.Status = TransactionStatus.Approved;
                transaction.TransactionType = TransactionType.ByAdmin;
                transaction.UserId = user.Id;
                transaction.ApprovedBy = user.UserName;

                await _clientService.AddUnit(id, transaction);

                TempData["Success"] = "Client's Unit Update was successful. Client: " + client.User.UserName + ", Units Added: " + model.Units + ".";
                return RedirectToAction("ClientDetails", new { id = client.UserId });
            }
            ViewBag.Client = client;
            return View(model);
        }

        #endregion Units

        #region Transactions

        public async Task<ActionResult> UserTransactions(int id)
        {
            var transactions = await _clientService.GetUserTransactions(id);
            return View(transactions);
        }

        #endregion Transactions


        #region Delete User

        public async Task<ActionResult> DeleteUser(string id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Adminpanel/BankDetails/Delete/5
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteUserConfirmed(string id)
        {
            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["error"] = "User not found";
                return RedirectToAction("Index");
            }
            var groups = db.Groups.Where(x => x.UserId == id).ToList();
            if (groups.Count() > 0)
            {
                foreach (var g in groups.ToList())
                {
                    var contacts = db.Contacts.Where(x => x.GroupId == g.GroupId).ToList();
                    if (contacts.Count() > 0)
                    {
                        foreach (var c in contacts.ToList())
                        {
                            db.Contacts.Remove(c);
                        }
                    }
                    db.Groups.Remove(g);
                }
               // await db.SaveChangesAsync();
            }
            //transactions
            var transaction = db.Transactions.Where(x => x.UserId == id).ToList();
            if (transaction.Count() > 0)
            {
                foreach (var g in transaction.ToList())
                {
                    db.Transactions.Remove(g);
                }
               // await db.SaveChangesAsync();
            }

            //vouchers
            var vouchers = db.Vouchers.Where(x => x.UserId == id).ToList();
            if (vouchers.Count() > 0)
            {
                foreach (var g in vouchers.ToList())
                {
                    db.Vouchers.Remove(g);
                }
               // await db.SaveChangesAsync();
            }

            //batch vouchers
            var batch = db.BatchVouchers.Where(x => x.UserId == id).ToList();
            if (batch.Count() > 0)
            {
                foreach (var g in batch.ToList())
                {
                    db.BatchVouchers.Remove(g);
                }
              //  await db.SaveChangesAsync();
            }

            //message
            var message = db.Messages.Where(x => x.UserId == id).ToList();
            if (message.Count() > 0)
            {
                foreach (var g in message.ToList())
                {
                    db.Messages.Remove(g);
                }
              //  await db.SaveChangesAsync();
            }

            //client
            try
            {


                var client = await db.Clients.FirstOrDefaultAsync(x => x.UserId == id);
                if (client != null)
                {
                    db.Clients.Remove(client);

                  //  await db.SaveChangesAsync();
                }
            }
            catch (Exception d)
            {

            }

            await db.SaveChangesAsync();
            //get client role
            try
            {
                List<string> roles = new List<string>();
                var role = await UserManager.GetRolesAsync(user);
                roles = role.ToList();

                foreach (var r in roles)
                {
                    await UserManager.RemoveFromRoleAsync(user, r);
                }
            }
            catch (Exception d)
            {

            }
            try
            {
                await UserManager.DeleteAsync(user);
            }
            catch (Exception d)
            {

            }




            return RedirectToAction("UserList");
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UserManager?.Dispose();
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }





}
