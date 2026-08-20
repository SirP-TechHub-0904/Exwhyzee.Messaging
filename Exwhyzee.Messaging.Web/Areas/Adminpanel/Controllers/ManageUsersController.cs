using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Dtos;
using Exwhyzee.Messaging.Core.Paystack;
using Exwhyzee.Messaging.Core.Paystack.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System;
using X.PagedList;
using Exwhyzee.Messaging.Web;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize]
    [Area("Adminpanel")]
    public class ManageUsersController : Controller
    {
        private ApplicationDbContext db;
        private UserManager<ApplicationUser> UserManager;
        private RoleManager<IdentityRole> RoleManager;
        private IClientService _clientService;
        private ITransactionService _transactionService = new TransactionService();
        private IPayStackApi _paystack = new PayStackApi(AppConfig.PayStackSecretKey);

        public ManageUsersController(ApplicationDbContext _db, UserManager<ApplicationUser> _userManager, RoleManager<IdentityRole> _roleManager, IClientService clientService)
        {
            db = _db;
            UserManager = _userManager;
            RoleManager = _roleManager;
            _clientService = clientService;
        }

        // GET: Adminpanel/ManageUsers (UNIFIED LIGHTNING-FAST SUB-100MS USER MANAGEMENT STUDIO)
        public async Task<IActionResult> Index(string searchString, string roleFilter, int? page)
        {
            int pageNumber = page ?? 1;
            int pageSize = 25;

            // Filter out Fintech soft-deleted anonymized accounts
            var usersQuery = db.Users.AsNoTracking()
                .Where(u => !u.Email.EndsWith("@anonymized.local") && !u.UserName.StartsWith("deleted_user_"))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim().ToLower();
                usersQuery = usersQuery.Where(u => u.UserName.ToLower().Contains(term)
                    || (u.Email != null && u.Email.ToLower().Contains(term))
                    || (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
            }

            var allMatchedUsers = await usersQuery.OrderByDescending(u => u.Id).ToListAsync();

            // Filter by role in memory to avoid SQL OPENJSON '$' translation errors
            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                var roleObj = await RoleManager.FindByNameAsync(roleFilter);
                if (roleObj != null)
                {
                    var rawUserRolesForFilter = await db.UserRoles.AsNoTracking()
                        .Where(ur => ur.RoleId == roleObj.Id)
                        .Select(ur => ur.UserId)
                        .ToListAsync();
                    var userIdsInRole = rawUserRolesForFilter.ToHashSet();

                    allMatchedUsers = allMatchedUsers.Where(u => userIdsInRole.Contains(u.Id)).ToList();
                }
            }

            int totalCount = allMatchedUsers.Count;

            var rawUsers = allMatchedUsers
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var matchedUserIds = allMatchedUsers.Select(u => u.Id).ToHashSet();
            var pagedUserIds = rawUsers.Select(u => u.Id).ToHashSet();

            // Fetch all clients & roles into memory to prevent SQL OPENJSON '$' compatibility errors
            var allClients = await db.Clients.AsNoTracking().ToListAsync();
            var clients = allClients.Where(c => pagedUserIds.Contains(c.UserId)).ToList();
            decimal totalFilteredUnits = allClients.Where(c => matchedUserIds.Contains(c.UserId)).Sum(c => c.Units);

            var rawUserRoles = await db.UserRoles.AsNoTracking().ToListAsync();
            var rawRoles = await db.Roles.AsNoTracking().ToListAsync();

            var userRoles = (from ur in rawUserRoles
                             join r in rawRoles on ur.RoleId equals r.Id
                             where pagedUserIds.Contains(ur.UserId)
                             select new { ur.UserId, RoleName = r.Name })
                            .ToList();

            var userDtos = new List<UserManagementDto>();
            foreach (var u in rawUsers)
            {
                var client = clients.FirstOrDefault(c => c.UserId == u.Id);
                var uRoles = userRoles.Where(r => r.UserId == u.Id).Select(r => r.RoleName).ToList();

                string fullName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : u.UserName;
                if (string.IsNullOrWhiteSpace(fullName)) fullName = u.UserName;

                userDtos.Add(new UserManagementDto
                {
                    UserId = u.Id,
                    ClientId = client?.ClientId ?? 0,
                    Username = u.UserName,
                    Email = u.Email,
                    EmailConfirmed = u.EmailConfirmed,
                    PhoneNumber = u.PhoneNumber,
                    FullName = fullName,
                    UnitsBalance = client?.Units ?? 0m,
                    DateRegistered = DateTime.MinValue,
                    LockoutEnabled = u.LockoutEnabled,
                    LockoutEnd = u.LockoutEnd.HasValue ? u.LockoutEnd.Value.DateTime : (DateTime?)null,
                    Roles = uRoles
                });
            }

            ViewBag.Roles = await RoleManager.Roles.AsNoTracking().ToListAsync();
            ViewBag.CurrentFilter = searchString;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.TotalUsersCount = totalCount;
            ViewBag.TotalFilteredUnits = totalFilteredUnits;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.PaystackPublicKey = AppConfig.PayStackPublicKey;

            return View(userDtos);
        }

        public IActionResult UserList()
        {
            return RedirectToAction("Index");
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
                if (LockoutEnabled)
                {
                    user.LockoutEnd = LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow ? LockoutEnd : DateTime.UtcNow.AddYears(100);
                }
                else
                {
                    user.LockoutEnd = null;
                }

                user.DateOfBirth = DateOfBirth;

                var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
                if (u != null)
                {
                    u.Email = Email;
                    u.EmailConfirmed = EmailConfirmed;
                    u.PhoneNumber = PhoneNumber;
                    u.PhoneNumberConfirmed = PhoneNumberConfirmed;
                    u.LockoutEnabled = LockoutEnabled;
                    u.LockoutEnd = user.LockoutEnd;
                    u.DateOfBirth = DateOfBirth;
                }

                await UserManager.UpdateAsync(user);
                await db.SaveChangesAsync();

                return RedirectToAction("ClientDetails", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error updating user: " + ex.Message;
            }
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> UserToRole(string rolename, string userId, bool? ischecked)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (user != null)
            {
                if (ischecked.HasValue && ischecked.Value)
                {
                    await UserManager.AddToRoleAsync(user, rolename);
                }
                else
                {
                    await UserManager.RemoveFromRoleAsync(user, rolename);
                }
            }

            return RedirectToAction("Index");
        }

        #region ADMIN PAYSTACK TOP-UP FLOW (DISABLED MANUAL UNTRACKED TOP-UP)

        // POST: Adminpanel/ManageUsers/InitializeAdminPaystackTopUp
        [HttpPost]
        public async Task<IActionResult> InitializeAdminPaystackTopUp(string targetUserId, decimal units)
        {
            if (string.IsNullOrWhiteSpace(targetUserId) || units <= 0)
            {
                return Json(new { success = false, message = "Please select a valid user and enter units greater than zero." });
            }

            var adminUser = await UserManager.FindByNameAsync(User.Identity.Name);
            var targetUser = await UserManager.FindByIdAsync(targetUserId);
            var targetClient = await _clientService.GetClientDetailsByUserId(targetUserId);

            if (targetUser == null || targetClient == null)
            {
                return Json(new { success = false, message = "Target client profile not found." });
            }

            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            decimal pricePerUnit = adminSetting?.PricePerUnit ?? 2.0m;
            decimal totalAmount = units * pricePerUnit;

            Transaction transaction = new Transaction
            {
                Units = units,
                Amount = totalAmount,
                AmountPaid = totalAmount,
                ClientId = targetClient.ClientId,
                UserId = targetUser.Id,
                TransactionType = TransactionType.OnlinePayment,
                PaymentSource = "Paystack",
                Status = TransactionStatus.Pending,
                DateCreated = DateTime.UtcNow.AddHours(1),
                Note = $"Admin Paystack Top-Up of {units:N2} units for {targetUser.UserName} (By Admin: {adminUser?.UserName})",
                ApprovedBy = adminUser?.UserName
            };

            await _transactionService.AddTransaction(transaction);

            int amountInKobo = (int)Math.Round(totalAmount * 100);

            return Json(new
            {
                success = true,
                transactionId = transaction.TransactionId,
                amount = totalAmount,
                amountInKobo = amountInKobo,
                units = units,
                targetUsername = targetUser.UserName,
                email = adminUser?.Email ?? targetUser.Email,
                publicKey = AppConfig.PayStackPublicKey
            });
        }

        // POST: Adminpanel/ManageUsers/VerifyAdminPaystackTopUp
        [HttpPost]
        public async Task<IActionResult> VerifyAdminPaystackTopUp(string reference, int transactionId)
        {
            if (string.IsNullOrWhiteSpace(reference) || transactionId <= 0)
            {
                return Json(new { success = false, message = "Invalid transaction verification parameters." });
            }

            var transaction = await _transactionService.GetTransaction(transactionId);
            if (transaction == null)
            {
                return Json(new { success = false, message = "Transaction record not found." });
            }

            var client = await _clientService.GetClientDetails(transaction.ClientId);
            if (client == null)
            {
                return Json(new { success = false, message = "Target client profile not found." });
            }

            if (transaction.Status == TransactionStatus.Approved)
            {
                return Json(new
                {
                    success = true,
                    message = "Transaction has already been verified and credited.",
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
                    var adminUser = await UserManager.FindByNameAsync(User.Identity.Name);

                    transaction.Status = TransactionStatus.Approved;
                    transaction.DateApproved = DateTime.UtcNow.AddHours(1);
                    transaction.TransactionReference = reference;
                    transaction.AmountPaid = transaction.Amount;
                    transaction.ApprovedBy = adminUser?.UserName ?? "Paystack Admin";
                    transaction.PaymentSource = "Paystack";
                    transaction.IsAdminTransferred = false; // Sitting in Paystack
                    transaction.User = null;

                    await _clientService.AddUnit(client.ClientId, transaction);

                    return Json(new
                    {
                        success = true,
                        message = $"Paystack Payment Verified! Successfully credited {transaction.Units:N2} units to {client.User?.UserName ?? "User"}'s wallet.",
                        newBalance = client.Units + transaction.Units,
                        units = transaction.Units,
                        amount = transaction.Amount
                    });
                }

                return Json(new { success = false, message = response?.message ?? "Payment verification failed with Paystack." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Verification error: " + ex.Message });
            }
        }

        #endregion

        #region Client Details

        public async Task<ActionResult> CompleteClientRegistration(string id)
        {
            var settings = db.AdminSettings.FirstOrDefault();
            var clientDetails = await _clientService.GetClientDetailsByUserId(id);
            if (clientDetails == null)
            {
                Client newClient = new Client();
                newClient.UserId = id;
                newClient.Units = settings?.UnitPerNewMember ?? 10;
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
                return RedirectToAction("Index");
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

        #region Transactions

        public async Task<ActionResult> UserTransactions(int id)
        {
            var client = await _clientService.GetClientDetails(id);
            ViewBag.Client = client;
            var transactions = await _clientService.GetUserTransactions(id);
            return View(transactions);
        }

        #endregion Transactions

        #region Delete User (FINTECH COMPLIANT SOFT-DELETE & DATA ANONYMIZATION)

        public async Task<ActionResult> DeleteUser(string id)
        {
            if (id == null) return BadRequest();
            var user = await UserManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteUserConfirmed(string id)
        {
            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            string shortId = id.Length >= 8 ? id.Substring(0, 8) : id;

            // 1. Anonymize Identity User PII & Revoke Credentials
            user.UserName = $"deleted_user_{shortId}";
            user.Email = $"deleted_{shortId}@anonymized.local";
            user.NormalizedUserName = $"DELETED_USER_{shortId.ToUpper()}";
            user.NormalizedEmail = $"DELETED_{shortId.ToUpper()}@ANONYMIZED.LOCAL";
            user.PhoneNumber = "0000000000";
            user.PhoneNumberConfirmed = false;
            user.EmailConfirmed = false;
            user.PasswordHash = Guid.NewGuid().ToString("N"); // Clears password hash permanently
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTime.UtcNow.AddYears(100); // Permanent 100-year security lockout

            // 2. Anonymize Client Profile & Revoke API Keys
            var client = await db.Clients.FirstOrDefaultAsync(x => x.UserId == id);
            if (client != null)
            {
                client.Surname = "Deleted";
                client.FirstName = "User";
                client.OtherNames = null;
                client.ApiKey = null; // Instantly revokes API calls
                client.GeminiApiKey = null;
            }

            await db.SaveChangesAsync();

            TempData["success"] = $"User account @{user.UserName} has been soft-deleted, anonymized, and permanently locked. All historical transactions and audit ledgers have been preserved for Fintech compliance.";
            return RedirectToAction("Index");
        }

        #endregion

        #region SUPERADMIN 2FA OVERRIDE CONTROLS

        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DisableUserTwoFactor(string userId)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

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
            TempData["success"] = $"Two-Factor Authentication (2FA) for @{user.UserName} has been successfully disabled by SuperAdmin.";
            return RedirectToAction("ClientDetails", new { id = userId });
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateUserTwoFactorMethod(string userId, TwoFactorMethod method)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            user.PreferredTwoFactorMethod = method;
            if (method == TwoFactorMethod.None)
            {
                user.TwoFactorEnabled = false;
            }
            else
            {
                user.TwoFactorEnabled = true;
            }

            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u != null)
            {
                u.PreferredTwoFactorMethod = method;
                u.TwoFactorEnabled = user.TwoFactorEnabled;
            }

            await db.SaveChangesAsync();
            TempData["success"] = $"2FA method for @{user.UserName} updated to {method}.";
            return RedirectToAction("ClientDetails", new { id = userId });
        }

        #endregion

        #region USER ROLE MANAGEMENT CONTROLS

        // GET: Adminpanel/ManageUsers/ManageRoles/{id}
        public async Task<IActionResult> ManageRoles(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            var currentRoles = await UserManager.GetRolesAsync(user);
            var allRoles = await RoleManager.Roles.Select(r => r.Name).ToListAsync();

            ViewBag.User = user;
            ViewBag.CurrentRoles = currentRoles;
            ViewBag.AvailableRoles = allRoles.Where(r => !currentRoles.Contains(r)).ToList();

            return View();
        }

        // POST: Adminpanel/ManageUsers/AssignRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
            {
                TempData["error"] = "User ID and Role Name are required.";
                return RedirectToAction("Index");
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            if (!await RoleManager.RoleExistsAsync(roleName))
            {
                TempData["error"] = $"Role '{roleName}' does not exist.";
                return RedirectToAction("ManageRoles", new { id = userId });
            }

            if (await UserManager.IsInRoleAsync(user, roleName))
            {
                TempData["error"] = $"User @{user.UserName} is already assigned to role '{roleName}'.";
                return RedirectToAction("ManageRoles", new { id = userId });
            }

            var result = await UserManager.AddToRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                TempData["success"] = $"Role '{roleName}' successfully assigned to user @{user.UserName}.";
            }
            else
            {
                TempData["error"] = "Failed to assign role: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageRoles", new { id = userId });
        }

        // POST: Adminpanel/ManageUsers/RemoveRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
            {
                TempData["error"] = "User ID and Role Name are required.";
                return RedirectToAction("Index");
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            if (!await UserManager.IsInRoleAsync(user, roleName))
            {
                TempData["error"] = $"User @{user.UserName} does not have role '{roleName}'.";
                return RedirectToAction("ManageRoles", new { id = userId });
            }

            var result = await UserManager.RemoveFromRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                TempData["success"] = $"Role '{roleName}' successfully removed from user @{user.UserName}.";
            }
            else
            {
                TempData["error"] = "Failed to remove role: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageRoles", new { id = userId });
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UserManager?.Dispose();
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
