using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Exwhyzee.Messaging.Core.Services
{
    public static class KycService
    {
        private static bool _schemaEnsured = false;
        private static readonly object _lock = new object();

        public static async Task EnsureSchemaAsync(ApplicationDbContext db)
        {
            if (_schemaEnsured) return;

            try
            {
                string sql = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserKycProfile]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserKycProfile](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] NVARCHAR(450) NULL,
        [ClientId] INT NULL,
        [ResidentialAddress] NVARCHAR(MAX) NULL,
        [City] NVARCHAR(200) NULL,
        [State] NVARCHAR(200) NULL,
        [Country] NVARCHAR(200) NULL,
        [IsPhoneVerified] BIT NOT NULL DEFAULT 0,
        [PhoneVerifiedDate] DATETIME2 NULL,
        [PhoneOtpCode] NVARCHAR(50) NULL,
        [PhoneOtpExpiry] DATETIME2 NULL,
        [PassportPhotoPath] NVARCHAR(MAX) NULL,
        [IdType] NVARCHAR(100) NULL,
        [IdNumber] NVARCHAR(200) NULL,
        [IdDocumentFrontPath] NVARCHAR(MAX) NULL,
        [IdDocumentBackPath] NVARCHAR(MAX) NULL,
        [KycStatus] INT NOT NULL DEFAULT 0,
        [TriggerReason] NVARCHAR(MAX) NULL,
        [AdminReviewNote] NVARCHAR(MAX) NULL,
        [TriggeredDate] DATETIME2 NULL,
        [SubmittedDate] DATETIME2 NULL,
        [ReviewedDate] DATETIME2 NULL,
        [ReviewedBy] NVARCHAR(256) NULL,
        [LastUpdated] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_UserKycProfile_UserId ON [dbo].[UserKycProfile]([UserId]);
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[KycSetting]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[KycSetting](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [IsKycEnabled] BIT NOT NULL DEFAULT 1,
        [TriggerRule] NVARCHAR(50) NULL DEFAULT 'Either',
        [SmsSentThreshold] INT NOT NULL DEFAULT 1000,
        [UnitsFundedThreshold] DECIMAL(18,2) NOT NULL DEFAULT 10000.00,
        [SingleTopUpUnitsThreshold] DECIMAL(18,2) NOT NULL DEFAULT 5000.00,
        [EnforceBlockOnCompose] BIT NOT NULL DEFAULT 0,
        [EnforceBlockOnFunding] BIT NOT NULL DEFAULT 0,
        [RequirePhoneVerification] BIT NOT NULL DEFAULT 1,
        [RequireAddress] BIT NOT NULL DEFAULT 1,
        [RequirePassportPhoto] BIT NOT NULL DEFAULT 1,
        [RequireGovernmentId] BIT NOT NULL DEFAULT 1,
        [AcceptedIdTypes] NVARCHAR(500) NULL DEFAULT 'NIN,VotersCard,DriversLicense,InternationalPassport,CAC',
        [LastUpdatedBy] NVARCHAR(256) NULL,
        [LastUpdatedDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END";
                await db.Database.ExecuteSqlRawAsync(sql);
                lock (_lock)
                {
                    _schemaEnsured = true;
                }
            }
            catch
            {
                // Ignore if already created or transient permission warning
            }
        }

        public static async Task<KycSetting> GetOrCreateSettingAsync(ApplicationDbContext db)
        {
            await EnsureSchemaAsync(db);
            var setting = await db.KycSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new KycSetting
                {
                    IsKycEnabled = true,
                    TriggerRule = "Either",
                    SmsSentThreshold = 1000,
                    UnitsFundedThreshold = 10000m,
                    SingleTopUpUnitsThreshold = 5000m,
                    EnforceBlockOnCompose = false,
                    EnforceBlockOnFunding = false,
                    RequirePhoneVerification = true,
                    RequireAddress = true,
                    RequirePassportPhoto = true,
                    RequireGovernmentId = true,
                    AcceptedIdTypes = "NIN,VotersCard,DriversLicense,InternationalPassport,CAC",
                    LastUpdatedBy = "System",
                    LastUpdatedDate = DateTime.UtcNow
                };
                db.KycSettings.Add(setting);
                await db.SaveChangesAsync();
            }
            return setting;
        }

        public static async Task<UserKycProfile> GetOrCreateUserKycAsync(ApplicationDbContext db, string userId, int? clientId = null)
        {
            await EnsureSchemaAsync(db);
            if (string.IsNullOrWhiteSpace(userId)) return new UserKycProfile();

            var profile = await db.UserKycProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (profile == null)
            {
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (!clientId.HasValue)
                {
                    var c = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
                    clientId = c?.ClientId;
                }

                profile = new UserKycProfile
                {
                    UserId = userId,
                    ClientId = clientId,
                    Country = "Nigeria",
                    IsPhoneVerified = user?.PhoneNumberConfirmed ?? false,
                    KycStatus = KycVerificationStatus.Unverified,
                    LastUpdated = DateTime.UtcNow
                };
                db.UserKycProfiles.Add(profile);
                await db.SaveChangesAsync();
            }
            return profile;
        }

        public static int CountRecipientsInString(string recipients)
        {
            if (string.IsNullOrWhiteSpace(recipients)) return 0;
            return recipients.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static async Task<(bool IsTriggered, bool IsBlocked, string Reason, UserKycProfile Profile)> EvaluateAndTriggerUserAsync(
            ApplicationDbContext db,
            string userId,
            int? clientId = null,
            decimal? currentTopUpUnits = null,
            int additionalSmsRecipients = 0)
        {
            await EnsureSchemaAsync(db);
            var setting = await GetOrCreateSettingAsync(db);
            var profile = await GetOrCreateUserKycAsync(db, userId, clientId);

            if (!setting.IsKycEnabled)
            {
                return (false, false, null, profile);
            }

            if (profile.KycStatus == KycVerificationStatus.Approved || profile.KycStatus == KycVerificationStatus.Exempted)
            {
                return (false, false, null, profile);
            }

            if (profile.KycStatus == KycVerificationStatus.PendingReview)
            {
                // Already submitted and awaiting admin review
                return (true, false, profile.TriggerReason ?? "KYC Pending Admin Review", profile);
            }

            // Compute user's total SMS recipients sent using lightweight in-row UnitsUsed & Response_page (avoids reading Recipients LOBs)
            var adminSetting = await db.AdminSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal flatUnits = adminSetting != null && adminSetting.FlatUnitsPerSms > 0 ? adminSetting.FlatUnitsPerSms : 2.0m;

            var userMessages = await db.Messages.AsNoTracking()
                .Where(m => m.UserId == userId && m.Status == MessageStatus.Sent)
                .Select(m => new { m.UnitsUsed, m.Response_page })
                .ToListAsync();

            int totalSmsSent = userMessages.Sum(m =>
            {
                int pages = m.Response_page > 0 ? m.Response_page : 1;
                decimal div = pages * flatUnits;
                return m.UnitsUsed > 0 && div > 0 ? Math.Max(1, (int)Math.Round(m.UnitsUsed / div)) : 1;
            }) + additionalSmsRecipients;

            if (!clientId.HasValue)
            {
                var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
                clientId = client?.ClientId;
            }

            decimal totalUnitsFunded = 0m;
            if (clientId.HasValue)
            {
                var fundedList = await db.Transactions.AsNoTracking()
                    .Where(t => t.ClientId == clientId.Value && t.Status == TransactionStatus.Approved)
                    .Select(t => t.Units)
                    .ToListAsync();
                totalUnitsFunded = fundedList.Sum();
            }
            if (currentTopUpUnits.HasValue && currentTopUpUnits.Value > 0)
            {
                totalUnitsFunded += currentTopUpUnits.Value;
            }

            bool smsCrossed = setting.SmsSentThreshold > 0 && totalSmsSent >= setting.SmsSentThreshold;
            bool unitsCrossed = setting.UnitsFundedThreshold > 0 && totalUnitsFunded >= setting.UnitsFundedThreshold;
            bool singleTopUpCrossed = setting.SingleTopUpUnitsThreshold > 0 && currentTopUpUnits.HasValue && currentTopUpUnits.Value >= setting.SingleTopUpUnitsThreshold;

            bool shouldTrigger = false;
            var reasons = new List<string>();

            string rule = (setting.TriggerRule ?? "Either").Trim();
            if (rule.Equals("SmsOnly", StringComparison.OrdinalIgnoreCase))
            {
                if (smsCrossed)
                {
                    shouldTrigger = true;
                    reasons.Add($"Total SMS Sent ({totalSmsSent:N0}) reached threshold ({setting.SmsSentThreshold:N0})");
                }
            }
            else if (rule.Equals("UnitsOnly", StringComparison.OrdinalIgnoreCase))
            {
                if (unitsCrossed)
                {
                    shouldTrigger = true;
                    reasons.Add($"Cumulative Units Funded ({totalUnitsFunded:N0}) reached threshold ({setting.UnitsFundedThreshold:N0})");
                }
                if (singleTopUpCrossed)
                {
                    shouldTrigger = true;
                    reasons.Add($"Single Top-Up ({currentTopUpUnits.Value:N0} Units) reached threshold ({setting.SingleTopUpUnitsThreshold:N0})");
                }
            }
            else if (rule.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                if (smsCrossed && (unitsCrossed || singleTopUpCrossed))
                {
                    shouldTrigger = true;
                    reasons.Add($"Both SMS Sent ({totalSmsSent:N0}) & Units Funded ({totalUnitsFunded:N0}) thresholds reached");
                }
            }
            else if (!rule.Equals("ManualOnly", StringComparison.OrdinalIgnoreCase)) // "Either"
            {
                if (smsCrossed)
                {
                    shouldTrigger = true;
                    reasons.Add($"Total SMS Sent ({totalSmsSent:N0}) reached threshold ({setting.SmsSentThreshold:N0})");
                }
                if (unitsCrossed)
                {
                    shouldTrigger = true;
                    reasons.Add($"Cumulative Units Funded ({totalUnitsFunded:N0}) reached threshold ({setting.UnitsFundedThreshold:N0})");
                }
                if (singleTopUpCrossed)
                {
                    shouldTrigger = true;
                    reasons.Add($"Single Top-Up ({currentTopUpUnits.Value:N0} Units) reached threshold ({setting.SingleTopUpUnitsThreshold:N0})");
                }
            }

            if (shouldTrigger && profile.KycStatus == KycVerificationStatus.Unverified)
            {
                profile.KycStatus = KycVerificationStatus.Triggered;
                profile.TriggerReason = string.Join(" | ", reasons);
                profile.TriggeredDate = DateTime.UtcNow;
                profile.LastUpdated = DateTime.UtcNow;
                await db.SaveChangesAsync();

                // Create in-app notification for the user
                try
                {
                    db.AppNotifications.Add(new AppNotification
                    {
                        UserId = userId,
                        Title = "Account KYC Upgrade Required",
                        Message = $"Your account has reached a volume milestone ({profile.TriggerReason}). Please complete your KYC verification (Phone, Address, Passport & ID) in My Profile.",
                        NotificationType = "KYC_TRIGGER",
                        ActionUrl = "/ClientPanel/Dashboard/Details",
                        IsRead = false,
                        DateCreated = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
                catch { }
            }

            bool isCurrentlyTriggeredOrRejected = profile.KycStatus == KycVerificationStatus.Triggered || profile.KycStatus == KycVerificationStatus.Rejected;
            return (isCurrentlyTriggeredOrRejected, isCurrentlyTriggeredOrRejected, profile.TriggerReason, profile);
        }

        public static async Task<int> RunRetroactiveScanAsync(ApplicationDbContext db)
        {
            await EnsureSchemaAsync(db);
            var setting = await GetOrCreateSettingAsync(db);
            if (!setting.IsKycEnabled) return 0;

            var clients = await db.Clients.AsNoTracking().ToListAsync();
            var existingProfiles = await db.UserKycProfiles.ToListAsync();
            var profileByUser = existingProfiles
                .Where(p => !string.IsNullOrEmpty(p.UserId))
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var adminSetting = await db.AdminSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal flatUnits = adminSetting != null && adminSetting.FlatUnitsPerSms > 0 ? adminSetting.FlatUnitsPerSms : 2.0m;

            var sentMessages = await db.Messages.AsNoTracking()
                .Where(m => m.Status == MessageStatus.Sent && m.UserId != null)
                .Select(m => new { m.UserId, m.UnitsUsed, m.Response_page })
                .ToListAsync();

            var smsByUser = sentMessages
                .GroupBy(m => m.UserId)
                .ToDictionary(g => g.Key, g => g.Sum(x =>
                {
                    int pages = x.Response_page > 0 ? x.Response_page : 1;
                    decimal div = pages * flatUnits;
                    return x.UnitsUsed > 0 && div > 0 ? Math.Max(1, (int)Math.Round(x.UnitsUsed / div)) : 1;
                }));

            var approvedTxns = await db.Transactions.AsNoTracking()
                .Where(t => t.Status == TransactionStatus.Approved)
                .Select(t => new { t.ClientId, t.Units })
                .ToListAsync();

            var unitsByClient = approvedTxns
                .GroupBy(t => t.ClientId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Units));

            int newlyTriggered = 0;
            string rule = (setting.TriggerRule ?? "Either").Trim();

            foreach (var c in clients)
            {
                if (string.IsNullOrWhiteSpace(c.UserId)) continue;
                profileByUser.TryGetValue(c.UserId, out var prof);
                if (prof != null && prof.KycStatus != KycVerificationStatus.Unverified) continue;

                int totalSms = smsByUser.TryGetValue(c.UserId, out var sCount) ? sCount : 0;
                decimal totalUnits = unitsByClient.TryGetValue(c.ClientId, out var uSum) ? uSum : 0m;

                bool smsCrossed = setting.SmsSentThreshold > 0 && totalSms >= setting.SmsSentThreshold;
                bool unitsCrossed = setting.UnitsFundedThreshold > 0 && totalUnits >= setting.UnitsFundedThreshold;

                bool trigger = false;
                var reasons = new List<string>();
                if (rule.Equals("SmsOnly", StringComparison.OrdinalIgnoreCase) && smsCrossed)
                {
                    trigger = true;
                    reasons.Add($"Total SMS Sent ({totalSms:N0}) >= {setting.SmsSentThreshold:N0}");
                }
                else if (rule.Equals("UnitsOnly", StringComparison.OrdinalIgnoreCase) && unitsCrossed)
                {
                    trigger = true;
                    reasons.Add($"Cumulative Units Funded ({totalUnits:N0}) >= {setting.UnitsFundedThreshold:N0}");
                }
                else if (rule.Equals("Both", StringComparison.OrdinalIgnoreCase) && smsCrossed && unitsCrossed)
                {
                    trigger = true;
                    reasons.Add($"SMS Sent ({totalSms:N0}) & Units Funded ({totalUnits:N0}) thresholds reached");
                }
                else if (rule.Equals("Either", StringComparison.OrdinalIgnoreCase) && (smsCrossed || unitsCrossed))
                {
                    trigger = true;
                    if (smsCrossed) reasons.Add($"Total SMS Sent ({totalSms:N0}) >= {setting.SmsSentThreshold:N0}");
                    if (unitsCrossed) reasons.Add($"Cumulative Units Funded ({totalUnits:N0}) >= {setting.UnitsFundedThreshold:N0}");
                }

                if (trigger)
                {
                    if (prof == null)
                    {
                        prof = new UserKycProfile
                        {
                            UserId = c.UserId,
                            ClientId = c.ClientId,
                            Country = "Nigeria",
                            KycStatus = KycVerificationStatus.Triggered,
                            TriggerReason = string.Join(" | ", reasons),
                            TriggeredDate = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow
                        };
                        db.UserKycProfiles.Add(prof);
                        profileByUser[c.UserId] = prof;
                    }
                    else
                    {
                        prof.KycStatus = KycVerificationStatus.Triggered;
                        prof.TriggerReason = string.Join(" | ", reasons);
                        prof.TriggeredDate = DateTime.UtcNow;
                        prof.LastUpdated = DateTime.UtcNow;
                    }
                    newlyTriggered++;
                }
            }

            if (newlyTriggered > 0)
            {
                await db.SaveChangesAsync();
            }

            return newlyTriggered;
        }
    }
}

