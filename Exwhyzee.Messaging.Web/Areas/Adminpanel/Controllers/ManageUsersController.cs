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
        private IPayStackApi _paystack => new PayStackApi(AppConfig.PayStackSecretKey);

        public ManageUsersController(ApplicationDbContext _db, UserManager<ApplicationUser> _userManager, RoleManager<IdentityRole> _roleManager, IClientService clientService)
        {
            db = _db;
            UserManager = _userManager;
            RoleManager = _roleManager;
            _clientService = clientService;
        }

        #region SHARED DATA ENGINE & STATS CALCULATOR

        private async Task<List<UserManagementDto>> BuildUserManagementListAsync()
        {
            await Exwhyzee.Messaging.Core.Services.KycService.EnsureSchemaAsync(db);

            var rawUsers = await db.Users.AsNoTracking()
                .Where(u => !u.Email.EndsWith("@anonymized.local") && !u.UserName.StartsWith("deleted_user_"))
                .OrderByDescending(u => u.DateRegitered)
                .ThenByDescending(u => u.Id)
                .ToListAsync();

            var allClients = await db.Clients.AsNoTracking().ToListAsync();
            var clientByUserId = allClients
                .Where(c => !string.IsNullOrEmpty(c.UserId))
                .GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var kycProfiles = await db.UserKycProfiles.AsNoTracking().ToListAsync();
            var kycByUserId = kycProfiles
                .Where(k => !string.IsNullOrEmpty(k.UserId))
                .GroupBy(k => k.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var rawUserRoles = await db.UserRoles.AsNoTracking().ToListAsync();
            var rawRoles = await db.Roles.AsNoTracking().ToListAsync();
            var roleNameById = rawRoles.ToDictionary(r => r.Id, r => r.Name);

            var rolesByUserId = rawUserRoles
                .Where(ur => roleNameById.ContainsKey(ur.RoleId))
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.Select(ur => roleNameById[ur.RoleId]).Distinct().ToList());

            var lastSentList = await db.Messages.AsNoTracking()
                .Where(m => m.UserId != null && (m.DeliveredDate != null || m.Scheduleddate != null))
                .GroupBy(m => m.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastSent = g.Max(m => m.DeliveredDate ?? m.Scheduleddate)
                })
                .ToListAsync();

            var lastSentByKey = lastSentList
                .Where(x => !string.IsNullOrEmpty(x.UserId))
                .ToDictionary(x => x.UserId, x => x.LastSent, StringComparer.OrdinalIgnoreCase);

            var lastTxList = await db.Transactions.AsNoTracking()
                .GroupBy(t => t.ClientId)
                .Select(g => new
                {
                    ClientId = g.Key,
                    LastTx = g.Max(t => t.DateCreated)
                })
                .ToListAsync();

            var lastTxByClientId = lastTxList.ToDictionary(x => x.ClientId, x => x.LastTx);

            DateTime oneMonthCutoff = DateTime.UtcNow.AddDays(-30);
            var list = new List<UserManagementDto>(rawUsers.Count);

            foreach (var u in rawUsers)
            {
                clientByUserId.TryGetValue(u.Id, out var client);
                rolesByUserId.TryGetValue(u.Id, out var uRoles);
                if (uRoles == null || uRoles.Count == 0)
                {
                    uRoles = new List<string> { "Client" };
                }

                string fullName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : u.UserName;
                if (string.IsNullOrWhiteSpace(fullName)) fullName = u.UserName;

                DateTime? lastSent = null;
                if (lastSentByKey.TryGetValue(u.Id, out var ls1)) lastSent = ls1;
                else if (!string.IsNullOrEmpty(u.UserName) && lastSentByKey.TryGetValue(u.UserName, out var ls2)) lastSent = ls2;
                else if (client != null && lastSentByKey.TryGetValue(client.ClientId.ToString(), out var ls3)) lastSent = ls3;

                DateTime? lastTx = null;
                if (client != null && lastTxByClientId.TryGetValue(client.ClientId, out var ltx) && ltx > new DateTime(1990, 1, 1))
                {
                    lastTx = ltx;
                }

                DateTime regDate = u.DateRegitered > new DateTime(1990, 1, 1) ? u.DateRegitered : DateTime.MinValue;

                DateTime? lastActivity = lastSent;
                if (lastTx.HasValue && (!lastActivity.HasValue || lastTx.Value > lastActivity.Value))
                {
                    lastActivity = lastTx.Value;
                }
                if (regDate > new DateTime(1990, 1, 1) && (!lastActivity.HasValue || regDate > lastActivity.Value))
                {
                    lastActivity = regDate;
                }

                bool isActiveUnder1M = lastActivity.HasValue && lastActivity.Value >= oneMonthCutoff;

                string gender = !string.IsNullOrWhiteSpace(u.Gender) ? u.Gender : "Unspecified";
                if (gender.Equals("male", StringComparison.OrdinalIgnoreCase)) gender = "Male";
                else if (gender.Equals("female", StringComparison.OrdinalIgnoreCase)) gender = "Female";

                kycByUserId.TryGetValue(u.Id, out var kyc);
                bool phoneVer = u.PhoneNumberConfirmed || (kyc != null && kyc.IsPhoneVerified);

                list.Add(new UserManagementDto
                {
                    UserId = u.Id,
                    ClientId = client?.ClientId ?? 0,
                    Username = u.UserName,
                    FullName = fullName,
                    FirstName = client?.FirstName ?? "",
                    Surname = client?.Surname ?? "",
                    OtherNames = client?.OtherNames ?? "",
                    Email = u.Email,
                    EmailConfirmed = u.EmailConfirmed,
                    PhoneNumber = u.PhoneNumber,
                    PhoneNumberConfirmed = phoneVer,
                    IsPhoneVerified = phoneVer,
                    Gender = gender,
                    UnitsBalance = client?.Units ?? 0m,
                    DateRegistered = regDate,
                    LastTimeSent = lastSent,
                    LastActivityDate = lastActivity,
                    IsActiveWithinOneMonth = isActiveUnder1M,
                    LockoutEnabled = u.LockoutEnabled,
                    LockoutEnd = u.LockoutEnd.HasValue ? u.LockoutEnd.Value.DateTime : (DateTime?)null,
                    TwoFactorEnabled = u.TwoFactorEnabled,
                    Roles = uRoles,
                    KycStatus = kyc?.KycStatus ?? KycVerificationStatus.Unverified,
                    IdType = kyc?.IdType,
                    ResidentialAddress = kyc?.ResidentialAddress
                });
            }

            return list;
        }

        private UserManagementStatsDto ComputeStats(IEnumerable<UserManagementDto> users)
        {
            var list = users as IList<UserManagementDto> ?? users.ToList();
            return new UserManagementStatsDto
            {
                TotalUsers = list.Count,
                ActiveUnderOneMonth = list.Count(u => u.IsActiveWithinOneMonth),
                ActiveOverOneMonth = list.Count(u => !u.IsActiveWithinOneMonth),
                MaleUsers = list.Count(u => string.Equals(u.Gender, "Male", StringComparison.OrdinalIgnoreCase)),
                FemaleUsers = list.Count(u => string.Equals(u.Gender, "Female", StringComparison.OrdinalIgnoreCase)),
                UnspecifiedGenderUsers = list.Count(u => !string.Equals(u.Gender, "Male", StringComparison.OrdinalIgnoreCase) && !string.Equals(u.Gender, "Female", StringComparison.OrdinalIgnoreCase)),
                ClientsOnlyCount = list.Count(u => !u.IsAdmin),
                AdminUsersCount = list.Count(u => u.IsAdmin),
                TotalUnitsBalance = list.Sum(u => u.UnitsBalance),
                UsersUnder1kUnits = list.Count(u => u.UnitsBalance < 1000m),
                UnitsSumUnder1k = list.Where(u => u.UnitsBalance < 1000m).Sum(u => u.UnitsBalance),
                Users1kTo5kUnits = list.Count(u => u.UnitsBalance >= 1000m && u.UnitsBalance <= 5000m),
                UnitsSum1kTo5k = list.Where(u => u.UnitsBalance >= 1000m && u.UnitsBalance <= 5000m).Sum(u => u.UnitsBalance),
                UsersOver5kUnits = list.Count(u => u.UnitsBalance > 5000m),
                UnitsSumOver5k = list.Where(u => u.UnitsBalance > 5000m).Sum(u => u.UnitsBalance),
                UsersZeroUnits = list.Count(u => u.UnitsBalance <= 0m),
                KycApprovedUsers = list.Count(u => u.KycStatus == KycVerificationStatus.Approved || u.KycStatus == KycVerificationStatus.Exempted),
                KycPendingReviewUsers = list.Count(u => u.KycStatus == KycVerificationStatus.PendingReview),
                KycTriggeredUsers = list.Count(u => u.KycStatus == KycVerificationStatus.Triggered || u.KycStatus == KycVerificationStatus.Rejected),
                PhoneVerifiedUsers = list.Count(u => u.IsPhoneVerified)
            };
        }

        private List<UserManagementDto> ApplyFiltersAndSort(
            IEnumerable<UserManagementDto> source,
            string searchString,
            string roleFilter,
            string cardFilter,
            string activityFilter,
            string genderFilter,
            string balanceTier,
            decimal? minUnits,
            decimal? maxUnits,
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy,
            bool dateAppliesToLastSent = false)
        {
            var query = source.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim().ToLowerInvariant();
                query = query.Where(u =>
                    (u.Username != null && u.Username.ToLowerInvariant().Contains(term)) ||
                    (u.FullName != null && u.FullName.ToLowerInvariant().Contains(term)) ||
                    (u.Email != null && u.Email.ToLowerInvariant().Contains(term)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.ToLowerInvariant().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                query = query.Where(u => u.Roles.Any(r => r.Equals(roleFilter.Trim(), StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(cardFilter))
            {
                switch (cardFilter.Trim().ToLowerInvariant())
                {
                    case "online":
                        var rawKeys = Exwhyzee.Messaging.Core.Services.LiveUserTracker.GetOnlineUsersList(45);
                        var onlineNames = rawKeys
                            .Select(k => k.Contains(':') ? k.Split(':')[0] : k)
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(User.Identity?.Name))
                        {
                            onlineNames.Add(User.Identity.Name);
                        }
                        query = query.Where(u =>
                            (u.Username != null && onlineNames.Contains(u.Username)) ||
                            (u.UserId != null && onlineNames.Contains(u.UserId)));
                        break;
                    case "active_under_1m":
                        query = query.Where(u => u.IsActiveWithinOneMonth);
                        break;
                    case "active_over_1m":
                        query = query.Where(u => !u.IsActiveWithinOneMonth);
                        break;
                    case "male":
                        query = query.Where(u => string.Equals(u.Gender, "Male", StringComparison.OrdinalIgnoreCase));
                        break;
                    case "female":
                        query = query.Where(u => string.Equals(u.Gender, "Female", StringComparison.OrdinalIgnoreCase));
                        break;
                    case "client_only":
                        query = query.Where(u => !u.IsAdmin);
                        break;
                    case "admins":
                        query = query.Where(u => u.IsAdmin);
                        break;
                    case "bal_under_1k":
                        query = query.Where(u => u.UnitsBalance < 1000m);
                        break;
                    case "bal_1k_5k":
                        query = query.Where(u => u.UnitsBalance >= 1000m && u.UnitsBalance <= 5000m);
                        break;
                    case "bal_over_5k":
                        query = query.Where(u => u.UnitsBalance > 5000m);
                        break;
                    case "bal_zero":
                        query = query.Where(u => u.UnitsBalance <= 0m);
                        break;
                    case "kyc_approved":
                        query = query.Where(u => u.KycStatus == KycVerificationStatus.Approved || u.KycStatus == KycVerificationStatus.Exempted);
                        break;
                    case "kyc_pending":
                        query = query.Where(u => u.KycStatus == KycVerificationStatus.PendingReview);
                        break;
                    case "kyc_triggered":
                        query = query.Where(u => u.KycStatus == KycVerificationStatus.Triggered || u.KycStatus == KycVerificationStatus.Rejected);
                        break;
                    case "phone_verified":
                        query = query.Where(u => u.IsPhoneVerified);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(activityFilter))
            {
                if (activityFilter == "under_1m") query = query.Where(u => u.IsActiveWithinOneMonth);
                else if (activityFilter == "over_1m") query = query.Where(u => !u.IsActiveWithinOneMonth);
            }

            if (!string.IsNullOrWhiteSpace(genderFilter))
            {
                query = query.Where(u => string.Equals(u.Gender, genderFilter.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(balanceTier))
            {
                switch (balanceTier.Trim().ToLowerInvariant())
                {
                    case "under_1k":
                        query = query.Where(u => u.UnitsBalance < 1000m);
                        break;
                    case "1k_5k":
                        query = query.Where(u => u.UnitsBalance >= 1000m && u.UnitsBalance <= 5000m);
                        break;
                    case "over_5k":
                        query = query.Where(u => u.UnitsBalance > 5000m);
                        break;
                    case "zero":
                        query = query.Where(u => u.UnitsBalance <= 0m);
                        break;
                }
            }

            if (minUnits.HasValue)
            {
                query = query.Where(u => u.UnitsBalance >= minUnits.Value);
            }
            if (maxUnits.HasValue)
            {
                query = query.Where(u => u.UnitsBalance <= maxUnits.Value);
            }

            if (fromDate.HasValue)
            {
                DateTime start = fromDate.Value.Date;
                if (dateAppliesToLastSent)
                    query = query.Where(u => u.LastTimeSent.HasValue && u.LastTimeSent.Value >= start);
                else
                    query = query.Where(u => u.DateRegistered >= start);
            }
            if (toDate.HasValue)
            {
                DateTime end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                if (dateAppliesToLastSent)
                    query = query.Where(u => u.LastTimeSent.HasValue && u.LastTimeSent.Value <= end);
                else
                    query = query.Where(u => u.DateRegistered > new DateTime(1990, 1, 1) && u.DateRegistered <= end);
            }

            switch ((sortBy ?? "").Trim().ToLowerInvariant())
            {
                case "name_asc":
                    query = query.OrderBy(u => u.FullName).ThenBy(u => u.Username);
                    break;
                case "name_desc":
                    query = query.OrderByDescending(u => u.FullName).ThenByDescending(u => u.Username);
                    break;
                case "balance_desc":
                    query = query.OrderByDescending(u => u.UnitsBalance).ThenBy(u => u.FullName);
                    break;
                case "balance_asc":
                    query = query.OrderBy(u => u.UnitsBalance).ThenBy(u => u.FullName);
                    break;
                case "date_asc":
                    query = query.OrderBy(u => u.DateRegistered == DateTime.MinValue ? DateTime.MaxValue : u.DateRegistered);
                    break;
                case "date_desc":
                    query = query.OrderByDescending(u => u.DateRegistered);
                    break;
                case "lastsent_desc":
                    query = query.OrderByDescending(u => u.LastTimeSent ?? DateTime.MinValue);
                    break;
                case "lastsent_asc":
                    query = query.OrderBy(u => u.LastTimeSent ?? DateTime.MaxValue);
                    break;
                case "activity_desc":
                    query = query.OrderByDescending(u => u.LastActivityDate ?? DateTime.MinValue);
                    break;
                default:
                    if (dateAppliesToLastSent)
                        query = query.OrderByDescending(u => u.UnitsBalance).ThenByDescending(u => u.LastTimeSent ?? DateTime.MinValue);
                    else
                        query = query.OrderByDescending(u => u.DateRegistered).ThenBy(u => u.FullName);
                    break;
            }

            return query.ToList();
        }

        #endregion

        // GET: Adminpanel/ManageUsers/Dashboard (MODULE 1: USER MANAGEMENT & UNITS EXECUTIVE DASHBOARD)
        public async Task<IActionResult> Dashboard()
        {
            var allUsers = await BuildUserManagementListAsync();
            var stats = ComputeStats(allUsers);
            return View(stats);
        }

        // GET: Adminpanel/ManageUsers (MODULE 2: MANAGE USERS STUDIO - WITHOUT UNITS COLUMN)
        public async Task<IActionResult> Index(
            string searchString,
            string roleFilter,
            string cardFilter,
            string activityFilter,
            string genderFilter,
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy,
            int? pageSize,
            int? page)
        {
            var allUsers = await BuildUserManagementListAsync();
            var globalStats = ComputeStats(allUsers);

            var filteredUsers = ApplyFiltersAndSort(
                allUsers,
                searchString,
                roleFilter,
                cardFilter,
                activityFilter,
                genderFilter,
                null,
                null,
                null,
                fromDate,
                toDate,
                sortBy,
                dateAppliesToLastSent: false);

            var filteredStats = ComputeStats(filteredUsers);
            int totalCount = filteredUsers.Count;

            int effectivePageSize = pageSize ?? 20;
            int pageNumber = page ?? 1;
            if (pageNumber < 1) pageNumber = 1;

            List<UserManagementDto> pagedUsers;
            int totalPages;

            if (effectivePageSize <= 0) // 0 = Show All
            {
                pagedUsers = filteredUsers;
                totalPages = 1;
                pageNumber = 1;
            }
            else
            {
                totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / effectivePageSize));
                if (pageNumber > totalPages) pageNumber = totalPages;

                pagedUsers = filteredUsers
                    .Skip((pageNumber - 1) * effectivePageSize)
                    .Take(effectivePageSize)
                    .ToList();
            }

            ViewBag.Roles = await RoleManager.Roles.AsNoTracking().ToListAsync();
            ViewBag.GlobalStats = globalStats;
            ViewBag.FilteredStats = filteredStats;
            ViewBag.CurrentFilter = searchString;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.CardFilter = cardFilter;
            ViewBag.ActivityFilter = activityFilter;
            ViewBag.GenderFilter = genderFilter;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.SortBy = sortBy ?? "date_desc";
            ViewBag.TotalUsersCount = totalCount;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = effectivePageSize;
            ViewBag.TotalPages = totalPages;

            return View(pagedUsers);
        }

        // GET: Adminpanel/ManageUsers/ManageUnits (MODULE 3: MANAGE UNITS STUDIO)
        public async Task<IActionResult> ManageUnits(
            string searchString,
            string cardFilter,
            string balanceTier,
            decimal? minUnits,
            decimal? maxUnits,
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy,
            int? pageSize,
            int? page)
        {
            var allUsers = await BuildUserManagementListAsync();
            var globalStats = ComputeStats(allUsers);

            var filteredUsers = ApplyFiltersAndSort(
                allUsers,
                searchString,
                null,
                cardFilter,
                null,
                null,
                balanceTier,
                minUnits,
                maxUnits,
                fromDate,
                toDate,
                sortBy ?? "balance_desc",
                dateAppliesToLastSent: true);

            var filteredStats = ComputeStats(filteredUsers);
            int totalCount = filteredUsers.Count;

            int effectivePageSize = pageSize ?? 20;
            int pageNumber = page ?? 1;
            if (pageNumber < 1) pageNumber = 1;

            List<UserManagementDto> pagedUsers;
            int totalPages;

            if (effectivePageSize <= 0) // 0 = Show All
            {
                pagedUsers = filteredUsers;
                totalPages = 1;
                pageNumber = 1;
            }
            else
            {
                totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / effectivePageSize));
                if (pageNumber > totalPages) pageNumber = totalPages;

                pagedUsers = filteredUsers
                    .Skip((pageNumber - 1) * effectivePageSize)
                    .Take(effectivePageSize)
                    .ToList();
            }

            ViewBag.GlobalStats = globalStats;
            ViewBag.FilteredStats = filteredStats;
            ViewBag.CurrentFilter = searchString;
            ViewBag.CardFilter = cardFilter;
            ViewBag.BalanceTier = balanceTier;
            ViewBag.MinUnits = minUnits;
            ViewBag.MaxUnits = maxUnits;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.SortBy = sortBy ?? "balance_desc";
            ViewBag.TotalUsersCount = totalCount;
            ViewBag.TotalFilteredUnits = filteredStats.TotalUnitsBalance;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = effectivePageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.PaystackPublicKey = AppConfig.PayStackPublicKey;

            return View(pagedUsers);
        }

        // GET: Adminpanel/ManageUsers/Export (EXCEL, WORD, PDF EXPORT ENGINE)
        public async Task<IActionResult> Export(
            string format,
            string mode,
            string searchString,
            string roleFilter,
            string cardFilter,
            string activityFilter,
            string genderFilter,
            string balanceTier,
            decimal? minUnits,
            decimal? maxUnits,
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy)
        {
            bool isUnitsMode = string.Equals(mode, "units", StringComparison.OrdinalIgnoreCase);
            var allUsers = await BuildUserManagementListAsync();

            var filteredUsers = ApplyFiltersAndSort(
                allUsers,
                searchString,
                roleFilter,
                cardFilter,
                activityFilter,
                genderFilter,
                balanceTier,
                minUnits,
                maxUnits,
                fromDate,
                toDate,
                sortBy,
                dateAppliesToLastSent: isUnitsMode);

            var stats = ComputeStats(filteredUsers);
            string fmt = (format ?? "excel").Trim().ToLowerInvariant();
            string timestamp = DateTime.UtcNow.AddHours(1).ToString("yyyyMMdd_HHmm");

            if (fmt == "pdf")
            {
                ViewBag.ExportTitle = isUnitsMode ? "User Wallet Units Report" : "Registered Users Directory Report";
                ViewBag.IsUnitsMode = isUnitsMode;
                ViewBag.Stats = stats;
                ViewBag.GeneratedAt = DateTime.UtcNow.AddHours(1).ToString("dd MMM yyyy, hh:mm tt");
                return View("ExportReport", filteredUsers);
            }

            var sb = new System.Text.StringBuilder();
            string reportTitle = isUnitsMode ? "Exwhyzee Bulk SMS - User Units Balance Report" : "Exwhyzee Bulk SMS - User Management Report";

            sb.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:x='urn:schemas-microsoft-com:office:excel' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>");
            sb.AppendLine("<head><meta charset='utf-8'/><style>");
            sb.AppendLine("body { font-family: Calibri, Arial, sans-serif; font-size: 11pt; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th { background-color: #0F172A; color: #FFFFFF; font-weight: bold; padding: 8px; border: 1px solid #CBD5E1; text-align: left; }");
            sb.AppendLine("td { padding: 6px 8px; border: 1px solid #CBD5E1; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(reportTitle)}</h2>");
            sb.AppendLine($"<p><strong>Generated:</strong> {DateTime.UtcNow.AddHours(1):dd MMM yyyy HH:mm} | <strong>Total Matched Users:</strong> {stats.TotalUsers:N0} | <strong>Total Units Sum:</strong> {stats.TotalUnitsBalance:N2} Units</p>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>S/N</th><th>Full Name</th><th>Username</th><th>Email</th><th>Phone Number</th>");

            if (isUnitsMode)
            {
                sb.AppendLine("<th>Unit Balance</th><th>Last SMS Sent</th>");
            }
            else
            {
                sb.AppendLine("<th>Gender</th><th>Roles</th><th>Email Verified</th><th>Date Registered</th><th>Activity Status</th>");
            }

            sb.AppendLine("</tr></thead><tbody>");

            int idx = 1;
            foreach (var u in filteredUsers)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{idx++}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(u.FullName)}</td>");
                sb.AppendLine($"<td>@{System.Net.WebUtility.HtmlEncode(u.Username)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(u.Email ?? "")}</td>");
                sb.AppendLine($"<td style='mso-number-format:\"\\@\"'>{System.Net.WebUtility.HtmlEncode(u.PhoneNumber ?? "")}</td>");

                if (isUnitsMode)
                {
                    sb.AppendLine($"<td>{u.UnitsBalance:N2}</td>");
                    string lastSentText = u.LastTimeSent.HasValue ? u.LastTimeSent.Value.ToString("yyyy-MM-dd HH:mm") : "Never Sent";
                    sb.AppendLine($"<td>{lastSentText}</td>");
                }
                else
                {
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(u.Gender)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(string.Join(", ", u.Roles))}</td>");
                    sb.AppendLine($"<td>{(u.EmailConfirmed ? "Verified" : "Unverified")}</td>");
                    string regText = u.DateRegistered > new DateTime(1990, 1, 1) ? u.DateRegistered.ToString("yyyy-MM-dd") : "N/A";
                    sb.AppendLine($"<td>{regText}</td>");
                    sb.AppendLine($"<td>{(u.IsActiveWithinOneMonth ? "Active (< 1M)" : "Over 1M")}</td>");
                }

                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());

            if (fmt == "word")
            {
                string fileName = $"{(isUnitsMode ? "ManageUnits" : "ManageUsers")}_{timestamp}.doc";
                return File(bytes, "application/msword", fileName);
            }
            else
            {
                string fileName = $"{(isUnitsMode ? "ManageUnits" : "ManageUsers")}_{timestamp}.xls";
                return File(bytes, "application/vnd.ms-excel", fileName);
            }
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
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["error"] = "User ID is required.";
                return RedirectToAction("Index");
            }

            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            var clientDetails = await _clientService.GetClientDetailsByUserId(id);
            if (clientDetails == null)
            {
                var settings = await db.AdminSettings.FirstOrDefaultAsync();
                clientDetails = new Client
                {
                    UserId = user.Id,
                    Units = settings?.UnitPerNewMember ?? 10m,
                    FirstName = user.UserName,
                    Surname = ""
                };
                db.Clients.Add(clientDetails);
                await db.SaveChangesAsync();
            }

            var userRoles = await UserManager.GetRolesAsync(user);
            if (userRoles == null || userRoles.Count == 0)
            {
                userRoles = new List<string> { "Client" };
            }

            var senderIds = await db.XyzSenderIDs.AsNoTracking()
                .Where(s => s.ClientId == clientDetails.ClientId)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var allClientTxns = await db.Transactions.AsNoTracking()
                .Where(t => t.ClientId == clientDetails.ClientId || t.UserId == user.Id)
                .OrderByDescending(t => t.TransactionId)
                .ToListAsync();

            var approvedTxns = allClientTxns.Where(t => t.Status == TransactionStatus.Approved || t.DateApproved != null).ToList();
            decimal totalFundedAmount = approvedTxns.Sum(t => t.AmountPaid.HasValue && t.AmountPaid.Value > 0 ? t.AmountPaid.Value : t.Amount);
            decimal totalUnitsPurchased = approvedTxns.Sum(t => t.Units);

            string clientIdStr = clientDetails.ClientId.ToString();
            var userMsgQuery = db.Messages.AsNoTracking()
                .Where(m => m.UserId == user.Id || m.UserId == user.UserName || m.UserId == clientIdStr);

            var msgScalars = await userMsgQuery
                .Select(m => new { m.UnitsUsed, m.DeliveredDate, m.Scheduleddate, m.Status, m.Response_page })
                .ToListAsync();

            var recentUserMsgs = await userMsgQuery
                .OrderByDescending(m => m.MessageId)
                .Take(10)
                .Select(m => new Message
                {
                    MessageId = m.MessageId,
                    SenderId = m.SenderId,
                    MessageContent = m.MessageContent,
                    UnitsUsed = m.UnitsUsed,
                    Status = m.Status,
                    DeliveredDate = m.DeliveredDate,
                    Scheduleddate = m.Scheduleddate
                })
                .ToListAsync();

            decimal totalUnitsBurned = msgScalars.Sum(m => m.UnitsUsed);
            DateTime? lastTimeSent = msgScalars
                .Select(m => m.DeliveredDate ?? m.Scheduleddate)
                .Where(d => d.HasValue)
                .OrderByDescending(d => d.Value)
                .FirstOrDefault();

            int contactsCount = await db.Contacts.AsNoTracking().CountAsync(c => c.Group != null && c.Group.UserId == user.Id);
            int groupsCount = await db.Groups.AsNoTracking().CountAsync(g => g.UserId == user.Id);

            var kycProfile = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateUserKycAsync(db, user.Id, clientDetails.ClientId);
            var kycSetting = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateSettingAsync(db);
            int totalSmsRecipientsSent = msgScalars
                .Where(m => m.Status == MessageStatus.Sent)
                .Sum(m =>
                {
                    int pages = m.Response_page > 0 ? m.Response_page : 1;
                    decimal div = pages * 2.0m;
                    return m.UnitsUsed > 0 ? Math.Max(1, (int)Math.Round(m.UnitsUsed / div)) : 1;
                });

            ViewBag.Details = user;
            ViewBag.UserRoles = userRoles;
            ViewBag.SenderIds = senderIds;
            ViewBag.RecentTransactions = allClientTxns.Take(10).ToList();
            ViewBag.TotalTransactionsCount = allClientTxns.Count;
            ViewBag.TotalFundedAmount = totalFundedAmount;
            ViewBag.TotalUnitsPurchased = totalUnitsPurchased;
            ViewBag.RecentMessages = recentUserMsgs;
            ViewBag.TotalBroadcastsCount = msgScalars.Count;
            ViewBag.TotalSmsRecipientsSent = totalSmsRecipientsSent;
            ViewBag.TotalUnitsBurned = totalUnitsBurned;
            ViewBag.LastTimeSent = lastTimeSent;
            ViewBag.ContactsCount = contactsCount;
            ViewBag.GroupsCount = groupsCount;
            ViewBag.PaystackPublicKey = AppConfig.PayStackPublicKey;
            ViewBag.KycProfile = kycProfile;
            ViewBag.KycSetting = kycSetting;

            return View(clientDetails);
        }

        #region KYC MANAGEMENT & TRIGGER SETTINGS

        [HttpGet]
        public async Task<IActionResult> KycSettings()
        {
            var setting = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateSettingAsync(db);
            var allUsers = await BuildUserManagementListAsync();
            ViewBag.Stats = ComputeStats(allUsers);
            return View(setting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KycSettings(KycSetting model, string[] selectedIdTypes)
        {
            var setting = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateSettingAsync(db);

            setting.IsKycEnabled = model.IsKycEnabled;
            setting.TriggerRule = string.IsNullOrWhiteSpace(model.TriggerRule) ? "Either" : model.TriggerRule.Trim();
            setting.SmsSentThreshold = Math.Max(0, model.SmsSentThreshold);
            setting.UnitsFundedThreshold = Math.Max(0m, model.UnitsFundedThreshold);
            setting.SingleTopUpUnitsThreshold = Math.Max(0m, model.SingleTopUpUnitsThreshold);
            setting.EnforceBlockOnCompose = model.EnforceBlockOnCompose;
            setting.EnforceBlockOnFunding = model.EnforceBlockOnFunding;
            setting.RequirePhoneVerification = model.RequirePhoneVerification;
            setting.RequireAddress = model.RequireAddress;
            setting.RequirePassportPhoto = model.RequirePassportPhoto;
            setting.RequireGovernmentId = model.RequireGovernmentId;

            if (selectedIdTypes != null && selectedIdTypes.Length > 0)
            {
                setting.AcceptedIdTypes = string.Join(",", selectedIdTypes);
            }
            else if (!string.IsNullOrWhiteSpace(model.AcceptedIdTypes))
            {
                setting.AcceptedIdTypes = model.AcceptedIdTypes.Trim();
            }

            setting.LastUpdatedBy = User.Identity?.Name ?? "Admin";
            setting.LastUpdatedDate = DateTime.UtcNow.AddHours(1);

            await db.SaveChangesAsync();
            TempData["Success"] = "Global KYC & Trigger Settings saved successfully.";
            return RedirectToAction("KycSettings");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunKycRetroactiveScan()
        {
            int count = await Exwhyzee.Messaging.Core.Services.KycService.RunRetroactiveScanAsync(db);
            TempData["Success"] = $"Retroactive KYC scan complete! {count:N0} user account(s) met the SMS Sent / Units Funded threshold and were flagged for KYC upgrade.";
            return RedirectToAction("KycSettings");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewUserKyc(string userId, string decision, string reviewNote)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["error"] = "Invalid user ID.";
                return RedirectToAction("Index");
            }

            var profile = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateUserKycAsync(db, userId);
            string adminName = User.Identity?.Name ?? "Admin";
            string dec = (decision ?? "").Trim().ToLowerInvariant();

            if (dec == "approve")
            {
                profile.KycStatus = KycVerificationStatus.Approved;
                profile.AdminReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? "KYC Verified & Approved by Admin." : reviewNote.Trim();
                profile.ReviewedDate = DateTime.UtcNow.AddHours(1);
                profile.ReviewedBy = adminName;

                try
                {
                    db.AppNotifications.Add(new AppNotification
                    {
                        UserId = userId,
                        Title = "KYC Verification Approved!",
                        Message = "Congratulations! Your identity & KYC documents have been verified and approved.",
                        NotificationType = "KYC_APPROVED",
                        ActionUrl = "/ClientPanel/Dashboard/Details",
                        IsRead = false,
                        DateCreated = DateTime.UtcNow
                    });
                }
                catch { }
                TempData["Success"] = "User KYC has been APPROVED.";
            }
            else if (dec == "reject")
            {
                profile.KycStatus = KycVerificationStatus.Rejected;
                profile.AdminReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? "KYC documents rejected. Please re-upload clear documents." : reviewNote.Trim();
                profile.ReviewedDate = DateTime.UtcNow.AddHours(1);
                profile.ReviewedBy = adminName;

                try
                {
                    db.AppNotifications.Add(new AppNotification
                    {
                        UserId = userId,
                        Title = "KYC Verification Action Required",
                        Message = $"Your KYC submission requires attention: {profile.AdminReviewNote}",
                        NotificationType = "KYC_REJECTED",
                        ActionUrl = "/ClientPanel/Dashboard/Details",
                        IsRead = false,
                        DateCreated = DateTime.UtcNow
                    });
                }
                catch { }
                TempData["Success"] = "User KYC has been REJECTED and user notified.";
            }
            else if (dec == "trigger")
            {
                profile.KycStatus = KycVerificationStatus.Triggered;
                profile.TriggerReason = string.IsNullOrWhiteSpace(reviewNote) ? $"Manually triggered by Admin ({adminName})" : reviewNote.Trim();
                profile.TriggeredDate = DateTime.UtcNow.AddHours(1);
                TempData["Success"] = "Mandatory KYC Upgrade has been triggered for this user.";
            }
            else if (dec == "exempt")
            {
                profile.KycStatus = KycVerificationStatus.Exempted;
                profile.AdminReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? $"Exempted from KYC by Admin ({adminName})" : reviewNote.Trim();
                profile.ReviewedDate = DateTime.UtcNow.AddHours(1);
                profile.ReviewedBy = adminName;
                TempData["Success"] = "User has been exempted from KYC triggers.";
            }

            profile.LastUpdated = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync();

            return RedirectToAction("ClientDetails", new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePhoneVerified(string userId, bool isVerified)
        {
            if (string.IsNullOrWhiteSpace(userId)) return RedirectToAction("Index");

            var user = await UserManager.FindByIdAsync(userId);
            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var profile = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateUserKycAsync(db, userId);

            if (user != null)
            {
                user.PhoneNumberConfirmed = isVerified;
                await UserManager.UpdateAsync(user);
            }
            if (dbUser != null)
            {
                dbUser.PhoneNumberConfirmed = isVerified;
            }

            profile.IsPhoneVerified = isVerified;
            profile.PhoneVerifiedDate = isVerified ? DateTime.UtcNow.AddHours(1) : (DateTime?)null;
            profile.LastUpdated = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync();

            TempData["Success"] = isVerified ? "Phone number marked as Verified." : "Phone number marked as Unverified.";
            return RedirectToAction("ClientDetails", new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminSaveUserKyc(
            string userId,
            string residentialAddress,
            string city,
            string state,
            string country,
            string idType,
            string idNumber,
            Microsoft.AspNetCore.Http.IFormFile passportFile,
            Microsoft.AspNetCore.Http.IFormFile idFrontFile,
            Microsoft.AspNetCore.Http.IFormFile idBackFile)
        {
            if (string.IsNullOrWhiteSpace(userId)) return RedirectToAction("Index");

            var profile = await Exwhyzee.Messaging.Core.Services.KycService.GetOrCreateUserKycAsync(db, userId);
            if (residentialAddress != null) profile.ResidentialAddress = residentialAddress.Trim();
            if (city != null) profile.City = city.Trim();
            if (state != null) profile.State = state.Trim();
            if (!string.IsNullOrWhiteSpace(country)) profile.Country = country.Trim();
            if (!string.IsNullOrWhiteSpace(idType)) profile.IdType = idType.Trim();
            if (idNumber != null) profile.IdNumber = idNumber.Trim();

            var env = HttpContext.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            var cloudinary = HttpContext.RequestServices.GetService<Exwhyzee.Messaging.Core.Services.ICloudinaryService>()
                ?? new Exwhyzee.Messaging.Core.Services.CloudinaryService(Exwhyzee.Messaging.Web.AppConfig.Configuration, env);
            string shortUser = !string.IsNullOrWhiteSpace(userId) ? userId.Substring(0, Math.Min(8, userId.Length)) : "user";

            if (passportFile != null && passportFile.Length > 0)
            {
                profile.PassportPhotoPath = await cloudinary.UploadFileAsync(passportFile, "kyc", $"passport_{shortUser}");
            }
            if (idFrontFile != null && idFrontFile.Length > 0)
            {
                profile.IdDocumentFrontPath = await cloudinary.UploadFileAsync(idFrontFile, "kyc", $"idfront_{shortUser}");
            }
            if (idBackFile != null && idBackFile.Length > 0)
            {
                profile.IdDocumentBackPath = await cloudinary.UploadFileAsync(idBackFile, "kyc", $"idback_{shortUser}");
            }

            profile.LastUpdated = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync();

            TempData["Success"] = "User KYC dossier & identity documents updated.";
            return RedirectToAction("ClientDetails", new { id = userId });
        }

        #endregion

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ClientDetails(
            Client model,
            string Gender,
            string Email,
            string PhoneNumber,
            bool? EmailConfirmed,
            bool? PhoneNumberConfirmed,
            DateTime? DateOfBirth,
            bool? LockoutEnabled)
        {
            await _clientService.UpdateClient(model);

            if (!string.IsNullOrWhiteSpace(model.UserId))
            {
                var user = await UserManager.FindByIdAsync(model.UserId);
                var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == model.UserId);

                if (user != null)
                {
                    if (!string.IsNullOrWhiteSpace(Email)) user.Email = Email.Trim();
                    if (PhoneNumber != null) user.PhoneNumber = PhoneNumber.Trim();
                    if (EmailConfirmed.HasValue) user.EmailConfirmed = EmailConfirmed.Value;
                    if (PhoneNumberConfirmed.HasValue) user.PhoneNumberConfirmed = PhoneNumberConfirmed.Value;
                    if (DateOfBirth.HasValue && DateOfBirth.Value > new DateTime(1900, 1, 1)) user.DateOfBirth = DateOfBirth.Value;
                    if (!string.IsNullOrWhiteSpace(Gender)) user.Gender = Gender.Trim();

                    if (LockoutEnabled.HasValue)
                    {
                        user.LockoutEnabled = LockoutEnabled.Value;
                        user.LockoutEnd = LockoutEnabled.Value ? DateTime.UtcNow.AddYears(100) : (DateTimeOffset?)null;
                    }

                    await UserManager.UpdateAsync(user);

                    if (dbUser != null)
                    {
                        dbUser.Email = user.Email;
                        dbUser.PhoneNumber = user.PhoneNumber;
                        dbUser.EmailConfirmed = user.EmailConfirmed;
                        dbUser.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
                        dbUser.DateOfBirth = user.DateOfBirth;
                        dbUser.Code = user.Code;
                        dbUser.LockoutEnabled = user.LockoutEnabled;
                        dbUser.LockoutEnd = user.LockoutEnd;
                        await db.SaveChangesAsync();
                    }
                }
            }

            TempData["Success"] = "Full user profile & settings updated successfully.";
            return RedirectToAction("ClientDetails", new { id = model.UserId });
        }

        #endregion Client Details

        #region Transactions

        private static string ResolveUserTranxBucket(Transaction t)
        {
            string src = (t.PaymentSource ?? "").Trim();
            if (src.IndexOf("Paystack", StringComparison.OrdinalIgnoreCase) >= 0 || t.TransactionType == TransactionType.OnlinePayment)
                return "Paystack";
            if (src.IndexOf("Bank", StringComparison.OrdinalIgnoreCase) >= 0 || t.TransactionType == TransactionType.BankDeposit)
                return "Bank Deposit";
            if (src.IndexOf("Voucher", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Voucher";
            return "Admin Top-Up";
        }

        private async Task<(Client ClientProfile, List<Transaction> Items, TransactionLedgerStatsDto Stats)> BuildFilteredUserTransactionsAsync(
            int clientId,
            string search,
            string statusFilter,
            string sourceFilter,
            string cardFilter,
            DateTime? dateFrom,
            DateTime? dateTo,
            string sortOrder)
        {
            var client = await db.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.ClientId == clientId);
            var rawList = await db.Transactions.AsNoTracking()
                .Where(t => t.ClientId == clientId)
                .ToListAsync();

            IEnumerable<Transaction> filtered = rawList;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLowerInvariant();
                string cleanId = s.TrimStart('#');
                filtered = filtered.Where(t =>
                    t.TransactionId.ToString().Contains(cleanId) ||
                    (t.TransactionReference ?? "").ToLowerInvariant().Contains(s) ||
                    (t.Note ?? "").ToLowerInvariant().Contains(s) ||
                    (t.ApprovedBy ?? "").ToLowerInvariant().Contains(s) ||
                    (t.PaymentSource ?? "").ToLowerInvariant().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<TransactionStatus>(statusFilter, true, out var parsedStatus))
                {
                    filtered = filtered.Where(t => t.Status == parsedStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(sourceFilter) && !sourceFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(t => string.Equals(ResolveUserTranxBucket(t), sourceFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (dateFrom.HasValue)
            {
                var fromDate = dateFrom.Value.Date;
                filtered = filtered.Where(t => t.DateCreated >= fromDate);
            }

            if (dateTo.HasValue)
            {
                var toDate = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                filtered = filtered.Where(t => t.DateCreated <= toDate);
            }

            if (!string.IsNullOrWhiteSpace(cardFilter) && !cardFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                switch (cardFilter.ToLowerInvariant())
                {
                    case "approved":
                        filtered = filtered.Where(t => t.Status == TransactionStatus.Approved);
                        break;
                    case "pending":
                        filtered = filtered.Where(t => t.Status == TransactionStatus.Pending);
                        break;
                    case "paystack":
                        filtered = filtered.Where(t => ResolveUserTranxBucket(t) == "Paystack");
                        break;
                    case "bank":
                        filtered = filtered.Where(t => ResolveUserTranxBucket(t) == "Bank Deposit");
                        break;
                    case "admin":
                        filtered = filtered.Where(t => ResolveUserTranxBucket(t) == "Admin Top-Up" || ResolveUserTranxBucket(t) == "Voucher");
                        break;
                    case "failed":
                        filtered = filtered.Where(t => t.Status == TransactionStatus.Failed || t.Status == TransactionStatus.Cancelled || t.Status == TransactionStatus.Revoked);
                        break;
                }
            }

            var resultList = filtered.ToList();
            var approvedItems = resultList.Where(t => t.Status == TransactionStatus.Approved).ToList();

            var stats = new TransactionLedgerStatsDto
            {
                TotalTransactions = resultList.Count,
                ApprovedCount = approvedItems.Count,
                ApprovedRevenueNgn = approvedItems.Sum(t => t.AmountPaid.HasValue && t.AmountPaid.Value > 0 ? t.AmountPaid.Value : t.Amount),
                ApprovedUnitsCredited = approvedItems.Sum(t => t.Units),
                PendingCount = resultList.Count(t => t.Status == TransactionStatus.Pending),
                PendingAmountNgn = resultList.Where(t => t.Status == TransactionStatus.Pending).Sum(t => t.AmountPaid.HasValue && t.AmountPaid.Value > 0 ? t.AmountPaid.Value : t.Amount),
                PaystackCount = resultList.Count(t => ResolveUserTranxBucket(t) == "Paystack"),
                PaystackRevenueNgn = approvedItems.Where(t => ResolveUserTranxBucket(t) == "Paystack").Sum(t => t.AmountPaid.HasValue && t.AmountPaid.Value > 0 ? t.AmountPaid.Value : t.Amount),
                BankDepositCount = resultList.Count(t => ResolveUserTranxBucket(t) == "Bank Deposit"),
                BankDepositRevenueNgn = approvedItems.Where(t => ResolveUserTranxBucket(t) == "Bank Deposit").Sum(t => t.AmountPaid.HasValue && t.AmountPaid.Value > 0 ? t.AmountPaid.Value : t.Amount),
                AdminTopUpCount = resultList.Count(t => ResolveUserTranxBucket(t) == "Admin Top-Up" || ResolveUserTranxBucket(t) == "Voucher"),
                AdminTopUpUnits = approvedItems.Where(t => ResolveUserTranxBucket(t) == "Admin Top-Up" || ResolveUserTranxBucket(t) == "Voucher").Sum(t => t.Units),
                FailedOrCancelledCount = resultList.Count(t => t.Status == TransactionStatus.Failed || t.Status == TransactionStatus.Cancelled || t.Status == TransactionStatus.Revoked)
            };

            resultList = (sortOrder ?? "date_desc") switch
            {
                "date_asc" => resultList.OrderBy(t => t.DateCreated).ThenBy(t => t.TransactionId).ToList(),
                "amount_desc" => resultList.OrderByDescending(t => t.AmountPaid ?? t.Amount).ThenByDescending(t => t.DateCreated).ToList(),
                "amount_asc" => resultList.OrderBy(t => t.AmountPaid ?? t.Amount).ThenByDescending(t => t.DateCreated).ToList(),
                "units_desc" => resultList.OrderByDescending(t => t.Units).ThenByDescending(t => t.DateCreated).ToList(),
                "units_asc" => resultList.OrderBy(t => t.Units).ThenByDescending(t => t.DateCreated).ToList(),
                _ => resultList.OrderByDescending(t => t.DateCreated).ThenByDescending(t => t.TransactionId).ToList()
            };

            return (client, resultList, stats);
        }

        public async Task<ActionResult> UserTransactions(
            int id,
            string search = null,
            string statusFilter = "all",
            string sourceFilter = "all",
            string cardFilter = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string sortOrder = "date_desc",
            int page = 1,
            int pageSize = 20)
        {
            var (client, allItems, stats) = await BuildFilteredUserTransactionsAsync(
                id, search, statusFilter, sourceFilter, cardFilter, dateFrom, dateTo, sortOrder);

            if (client == null)
            {
                TempData["error"] = "Client profile not found.";
                return RedirectToAction("Index");
            }

            int totalCount = allItems.Count;
            if (page < 1) page = 1;
            int effectivePageSize = pageSize <= 0 ? (totalCount > 0 ? totalCount : 1) : pageSize;
            int totalPages = (int)Math.Ceiling((double)totalCount / effectivePageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var pagedItems = pageSize <= 0
                ? allItems
                : allItems.Skip((page - 1) * effectivePageSize).Take(effectivePageSize).ToList();

            ViewBag.Client = client;
            ViewBag.Stats = stats;
            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.SourceFilter = sourceFilter;
            ViewBag.CardFilter = cardFilter;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.SortOrder = sortOrder;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(pagedItems);
        }

        [HttpGet]
        public async Task<IActionResult> ExportUserTransactions(
            int id,
            string format = "excel",
            string search = null,
            string statusFilter = "all",
            string sourceFilter = "all",
            string cardFilter = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string sortOrder = "date_desc")
        {
            var (client, items, stats) = await BuildFilteredUserTransactionsAsync(
                id, search, statusFilter, sourceFilter, cardFilter, dateFrom, dateTo, sortOrder);

            string clientName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : $"Client #{id}";
            if (string.IsNullOrWhiteSpace(clientName)) clientName = client?.User?.UserName ?? $"Client #{id}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8'/><style>");
            sb.AppendLine("body { font-family: Calibri, Arial, sans-serif; font-size: 11pt; color: #0f172a; margin: 20px; }");
            sb.AppendLine("h2 { margin-bottom: 4px; }");
            sb.AppendLine(".meta { color: #475569; font-size: 10pt; margin-bottom: 14px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th { background: #0f172a; color: #ffffff; font-weight: bold; padding: 8px; border: 1px solid #cbd5e1; text-align: left; }");
            sb.AppendLine("td { padding: 7px 8px; border: 1px solid #cbd5e1; vertical-align: top; }");
            sb.AppendLine("@media print { .no-print { display: none !important; } }");
            sb.AppendLine("</style></head>");

            bool isPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine(isPdf ? "<body onload='window.print()'>" : "<body>");
            if (isPdf)
            {
                sb.AppendLine("<div class='no-print' style='text-align:right;margin-bottom:10px;'><button onclick='window.print()' style='padding:8px 16px;background:#0284c7;color:#fff;border:none;border-radius:6px;font-weight:bold;cursor:pointer;'>Print / Save PDF</button></div>");
            }
            sb.AppendLine($"<h2>Client Transaction Ledger — {System.Net.WebUtility.HtmlEncode(clientName)} (@{System.Net.WebUtility.HtmlEncode(client?.User?.UserName ?? "N/A")})</h2>");
            sb.AppendLine($"<div class='meta'>Client ID: <strong>#{id}</strong> | Current Balance: <strong>{(client?.Units ?? 0m):N2} Units</strong> | Filtered Transactions: <strong>{stats.TotalTransactions:N0}</strong> | Approved Revenue: <strong>&#8358;{stats.ApprovedRevenueNgn:N2}</strong> | Approved Units: <strong>{stats.ApprovedUnitsCredited:N2}</strong></div>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>#</th><th>Trans ID</th><th>Date Created</th><th>Source &amp; Type</th><th>Reference &amp; Note</th><th>Amount Paid (&#8358;)</th><th>Units Credited</th><th>Approved Info</th><th>Status</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int sn = 1;
            foreach (var item in items)
            {
                decimal paidAmt = item.AmountPaid.HasValue && item.AmountPaid.Value > 0 ? item.AmountPaid.Value : item.Amount;
                string bucket = ResolveUserTranxBucket(item);
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{sn++}</td>");
                sb.AppendLine($"<td>#{item.TransactionId}</td>");
                sb.AppendLine($"<td>{item.DateCreated:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(bucket)} ({item.TransactionType})</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.TransactionReference ?? "-")} {(string.IsNullOrWhiteSpace(item.Note) ? "" : "— " + System.Net.WebUtility.HtmlEncode(item.Note))}</td>");
                sb.AppendLine($"<td>&#8358;{paidAmt:N2}</td>");
                sb.AppendLine($"<td>+{item.Units:N2}</td>");
                sb.AppendLine($"<td>{(item.DateApproved.HasValue ? item.DateApproved.Value.ToString("yyyy-MM-dd HH:mm") + " (" + System.Net.WebUtility.HtmlEncode(item.ApprovedBy ?? "System") + ")" : "Pending")}</td>");
                sb.AppendLine($"<td><strong>{item.Status}</strong></td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");

            if (isPdf)
            {
                return Content(sb.ToString(), "text/html", System.Text.Encoding.UTF8);
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            string timestamp = DateTime.UtcNow.AddHours(1).ToString("yyyyMMdd_HHmm");

            if (string.Equals(format, "word", StringComparison.OrdinalIgnoreCase))
            {
                return File(bytes, "application/msword", $"Client_{id}_Transactions_{timestamp}.doc");
            }

            return File(bytes, "application/vnd.ms-excel", $"Client_{id}_Transactions_{timestamp}.xls");
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
                client.Units = 0m;
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

        #region ADMIN PASSWORD RESET STUDIO

        // GET: Adminpanel/ManageUsers/AdminResetPassword/{id}
        public async Task<IActionResult> AdminResetPassword(string id)
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

            ViewBag.User = user;
            return View();
        }

        // POST: Adminpanel/ManageUsers/AdminResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminResetPassword(string userId, string newPassword, bool sendEmailNotification)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["error"] = "User ID and New Password are required.";
                return RedirectToAction("Index");
            }

            if (newPassword.Length < 6)
            {
                TempData["error"] = "Password must be at least 6 characters long.";
                return RedirectToAction("AdminResetPassword", new { id = userId });
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["error"] = "User account not found.";
                return RedirectToAction("Index");
            }

            if (await UserManager.HasPasswordAsync(user))
            {
                var removeResult = await UserManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded)
                {
                    TempData["error"] = "Error clearing previous password: " + string.Join(", ", removeResult.Errors.Select(e => e.Description));
                    return RedirectToAction("AdminResetPassword", new { id = userId });
                }
            }

            var addResult = await UserManager.AddPasswordAsync(user, newPassword);
            if (!addResult.Succeeded)
            {
                TempData["error"] = "Failed to set new password: " + string.Join(", ", addResult.Errors.Select(e => e.Description));
                return RedirectToAction("AdminResetPassword", new { id = userId });
            }

            await UserManager.UpdateSecurityStampAsync(user);

            string emailStatusNote = "";
            if (sendEmailNotification && !string.IsNullOrWhiteSpace(user.Email))
            {
                var zeptoMail = HttpContext.RequestServices.GetService<Exwhyzee.Messaging.Core.Services.IZeptoMailService>();
                if (zeptoMail != null)
                {
                    var subject = "Your Account Password Has Been Reset by Admin";
                    var body = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 14px rgba(0,0,0,0.06);"">
    <div style=""background: #0B0F19; padding: 24px 28px; text-align: center; border-bottom: 3px solid #E50914;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 18px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 12px;"">
            <img src=""{{LogoUrl}}"" alt=""Exwhyzee Bulk SMS"" style=""height: 38px; width: auto; display: block; margin: 0 auto;"" />
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
            Password Update Notice
        </h1>
    </div>
    <div style=""padding: 32px 28px; color: #1e293b; line-height: 1.6;"">
        <p style=""font-size: 15px; margin-top: 0;"">Hello <strong>{user.UserName}</strong>,</p>
        <p style=""color: #475569; font-size: 14px;"">
            Your <strong>Exwhyzee Bulk SMS</strong> account password has been reset by a system administrator.
        </p>
        <div style=""background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 8px; padding: 18px; margin: 20px 0; text-align: center;"">
            <small style=""display:block; color:#64748B; font-weight:700; text-transform:uppercase; margin-bottom:6px;"">Your New Temporary Password</small>
            <div style=""font-family: monospace; font-size: 20px; font-weight: 800; color: #0F172A; letter-spacing: 2px;"">
                {newPassword}
            </div>
        </div>
        <p style=""color: #64748B; font-size: 13px;"">
            For security, please log in and change your password immediately from your profile settings.
        </p>
        <div style=""text-align: center; margin-top: 24px;"">
            <a href=""https://xyzsms.com/Account/Login"" style=""display: inline-block; background: #E50914; color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 6px; font-weight: 700; font-size: 14px;"">
                Log In Now
            </a>
        </div>
    </div>
    <div style=""background: #f8fafc; padding: 14px 28px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9;"">
        &copy; {DateTime.UtcNow.Year} xyzsms.com
    </div>
</div>";
                    var sent = await zeptoMail.SendEmailAsync(body, user.Email, subject);
                    emailStatusNote = sent ? " A notification email with the new credentials was dispatched to the user." : " (Email notification could not be delivered).";
                }
            }

            TempData["success"] = $"Password for user @{user.UserName} was successfully reset.{emailStatusNote}";
            return RedirectToAction("ClientDetails", new { id = userId });
        }

        // POST: Adminpanel/ManageUsers/QuickResetPassword (AJAX)
        [HttpPost]
        public async Task<IActionResult> QuickResetPassword(string userId, string newPassword, bool sendEmailNotification)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newPassword))
            {
                return Json(new { success = false, message = "User ID and New Password are required." });
            }

            if (newPassword.Length < 6)
            {
                return Json(new { success = false, message = "Password must be at least 6 characters long." });
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User account not found." });
            }

            if (await UserManager.HasPasswordAsync(user))
            {
                var removeResult = await UserManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded)
                {
                    return Json(new { success = false, message = "Error clearing old password: " + string.Join(", ", removeResult.Errors.Select(e => e.Description)) });
                }
            }

            var addResult = await UserManager.AddPasswordAsync(user, newPassword);
            if (!addResult.Succeeded)
            {
                return Json(new { success = false, message = "Error setting new password: " + string.Join(", ", addResult.Errors.Select(e => e.Description)) });
            }

            await UserManager.UpdateSecurityStampAsync(user);

            bool emailSent = false;
            if (sendEmailNotification && !string.IsNullOrWhiteSpace(user.Email))
            {
                var zeptoMail = HttpContext.RequestServices.GetService<Exwhyzee.Messaging.Core.Services.IZeptoMailService>();
                if (zeptoMail != null)
                {
                    var subject = "Your Account Password Has Been Reset by Admin";
                    var body = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 14px rgba(0,0,0,0.06);"">
    <div style=""background: #0B0F19; padding: 24px 28px; text-align: center; border-bottom: 3px solid #E50914;"">
        <div style=""display: inline-block; background: #ffffff; padding: 8px 18px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.15); margin-bottom: 12px;"">
            <img src=""{{LogoUrl}}"" alt=""Exwhyzee Bulk SMS"" style=""height: 38px; width: auto; display: block; margin: 0 auto;"" />
        </div>
        <h1 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">
            Password Update Notice
        </h1>
    </div>
    <div style=""padding: 32px 28px; color: #1e293b; line-height: 1.6;"">
        <p style=""font-size: 15px; margin-top: 0;"">Hello <strong>{user.UserName}</strong>,</p>
        <p style=""color: #475569; font-size: 14px;"">
            Your <strong>Exwhyzee Bulk SMS</strong> account password has been reset by an administrator.
        </p>
        <div style=""background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 8px; padding: 18px; margin: 20px 0; text-align: center;"">
            <small style=""display:block; color:#64748B; font-weight:700; text-transform:uppercase; margin-bottom:6px;"">Your New Password</small>
            <div style=""font-family: monospace; font-size: 20px; font-weight: 800; color: #0F172A; letter-spacing: 2px;"">
                {newPassword}
            </div>
        </div>
        <p style=""color: #64748B; font-size: 13px;"">
            Please log in and update your password if desired.
        </p>
    </div>
    <div style=""background: #f8fafc; padding: 14px 28px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9;"">
        &copy; {DateTime.UtcNow.Year} xyzsms.com
    </div>
</div>";
                    emailSent = await zeptoMail.SendEmailAsync(body, user.Email, subject);
                }
            }

            return Json(new
            {
                success = true,
                message = $"Password for @{user.UserName} successfully reset!" + (sendEmailNotification ? (emailSent ? " Notification email dispatched." : " (Email could not be delivered).") : "")
            });
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
