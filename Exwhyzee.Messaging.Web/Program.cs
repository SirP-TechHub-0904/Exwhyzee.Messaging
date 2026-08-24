using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Exwhyzee.Messaging.Core.Models;

// Load local .env file into environment variables if present
var possibleEnvFiles = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "Exwhyzee.Messaging.Web", ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".env"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Exwhyzee.Messaging.Web", ".env")
};

foreach (var envFile in possibleEnvFiles)
{
    if (File.Exists(envFile))
    {
        foreach (var rawLine in File.ReadAllLines(envFile))
        {
            var line = rawLine.Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#") && line.Contains('='))
            {
                var parts = line.Split(new[] { '=' }, 2);
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

Exwhyzee.Messaging.Web.AppConfig.Configuration = builder.Configuration;

// Register Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ZyxsmsDbConnection")));

// Register Identity Services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Default password settings (you can customize these later)
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();

// Register Core Messaging Services
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.IAddressBookService, Exwhyzee.Messaging.Core.Data.Services.AddressBookService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.AddressBookService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.IClientService, Exwhyzee.Messaging.Core.Data.Services.ClientService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.ClientService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.IPriceSettingsService, Exwhyzee.Messaging.Core.Data.Services.PriceSettingService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.PriceSettingService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.ITransactionService, Exwhyzee.Messaging.Core.Data.Services.TransactionService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.TransactionService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.IVoucherService, Exwhyzee.Messaging.Core.Data.Services.VoucherService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.VoucherService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.IPaystackTransferService, Exwhyzee.Messaging.Core.Services.PaystackTransferService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.PaystackTransferService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.IAdminSettings, Exwhyzee.Messaging.Core.Data.Services.AdminSettings>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.AdminSettings>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.IApiSettings, Exwhyzee.Messaging.Core.Data.Services.ApiSettings>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.Services.ApiSettings>();

// Register Email Services (ZeptoMail with MailKit/MimeKit)
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.IZeptoMailService, Exwhyzee.Messaging.Core.Services.ZeptoMailService>();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Data.IServices.ISendEmail, Exwhyzee.Messaging.Core.Services.ZeptoMailService>();

// Register OTP Service
builder.Services.AddSingleton<Exwhyzee.Messaging.Core.Services.IOtpService, Exwhyzee.Messaging.Core.Services.OtpService>();

// Register Google reCAPTCHA Service
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.IGoogleReCaptchaService, Exwhyzee.Messaging.Core.Services.GoogleReCaptchaService>();

// Register In-App Notification & OneSignal Service
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.INotificationService, Exwhyzee.Messaging.Core.Services.NotificationService>();

// Register HttpClient & Google Gemini AI Contact Extractor Service
builder.Services.AddHttpClient();
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.IGeminiContactExtractorService, Exwhyzee.Messaging.Core.Services.GeminiContactExtractorService>();

// Register Scheduled Message Queue Background Worker
builder.Services.AddHostedService<Exwhyzee.Messaging.Core.Services.ScheduledMessageProcessorBackgroundService>();

// Register Low Unit Alert Service & Daily Morning Checker Background Worker
builder.Services.AddScoped<Exwhyzee.Messaging.Core.Services.ILowUnitAlertService, Exwhyzee.Messaging.Core.Services.LowUnitAlertService>();
builder.Services.AddHostedService<Exwhyzee.Messaging.Core.Services.DailyLowUnitCheckerBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Must be before Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
