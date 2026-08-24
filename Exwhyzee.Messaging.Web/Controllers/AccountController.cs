using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;




using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


namespace Exwhyzee.Messaging.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> _signInManager;
        private Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;
        private Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> _roleManager;
        private ISendEmail _email => HttpContext?.RequestServices?.GetService<ISendEmail>() ?? new SendEmail();
        private IDashboardService _dashboardService => HttpContext?.RequestServices?.GetService<IDashboardService>() ?? new DashboardService();
        private IGoogleReCaptchaService _reCaptchaService => HttpContext?.RequestServices?.GetService<IGoogleReCaptchaService>() ?? new GoogleReCaptchaService();
        private ITwoFactorService _twoFactorService => HttpContext?.RequestServices?.GetService<ITwoFactorService>() ?? new TwoFactorService();


        public AccountController(Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager, Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> signInManager, Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> roleManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
            RoleManager = roleManager;
         }

        public Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> SignInManager
        {
            get { return _signInManager; }
            private set
            {
                _signInManager = value;
            }
        }

        public Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> RoleManager
        {
            get { return _roleManager; }
            private set
            {
                _roleManager = value;
            }
        }

        public Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> UserManager
        {
            get { return _userManager; }
            private set
            {
                _userManager = value;
            }
        }

        #region
        //
        // GET: /Account/Login
        [AllowAnonymous]
        
        public ActionResult LoginAfter(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }


        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> LoginAfter(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "incorrect username or password";
                return View(model);
            }
            //
            //Check if User Mail has been verified
            var user = await UserManager.FindByNameAsync(model.UserName);
            if (user == null)
            {



                //if (user != null)
                //{
                //    if (!(await UserManager.IsEmailConfirmedAsync(user.Id)))
                //    {
                //        ModelState.AddModelError("", "Verification Error. Invalid login attempt.");
                //        return View(model);
                //    }
                //}
                //else
                //{
                if (await JosClient.CheckUserExistInJosUser(model.UserName, model.Password) == true)
                {
                    var encUserName = EncString.Encrypt(model.UserName, "ladybee");
                    var encpassword = EncString.Encrypt(model.Password, "ladybee");
                    //return RedirectToAction("MigrateAccount", new { username = encUserName, password = encpassword });
                    //redirect to another page
                    bool checkMig = await MigrateAccount(encUserName, encpassword);

                    if (checkMig == true)
                    {
                        // return RedirectToAction("GoToMail");
                    var signInResult = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                    if (signInResult.Succeeded)
                    {
                        if (returnUrl != null)
                        {
                            return RedirectToLocal(returnUrl);
                        }
                        else
                        {
                            if (User.IsInRole("Admin"))
                            {
                                return RedirectToAction("Index", "Main", new { @area = "AdminPanel" });
                            }
                            else
                            {
                                return RedirectToAction("Index", "Dashboard", new { @area = "ClientPanel" });
                            }
                        }
                    }
                    if (signInResult.IsLockedOut)
                    {
                        return View("Lockout");
                    }
                    if (signInResult.RequiresTwoFactor)
                    {
                        return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                    }
                    ModelState.AddModelError("", "Invalid login attempt.");
                    string messages = string.Join("; ", ModelState.Values
                                    .SelectMany(x => x.Errors)
                                    .Select(x => x.ErrorMessage));
                    return View(model);

                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid account migration and login attempt.");
                    }
                }
                //}
            }
            else
            {


                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, change lockoutOnFailure: true
                var signInResult = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (signInResult.Succeeded)
                {
                    if (returnUrl != null)
                    {
                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        if (User.IsInRole("Admin"))
                        {
                            return RedirectToAction("Index", "Main", new { @area = "AdminPanel" });
                        }
                        else
                        {
                            return RedirectToAction("Index", "Dashboard", new { @area = "ClientPanel" });
                        }
                    }
                }
                if (signInResult.IsLockedOut)
                    return View("Lockout");
                if (signInResult.RequiresTwoFactor)
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });

                ModelState.AddModelError("", "Invalid login attempt.");
                string messages = string.Join("; ", ModelState.Values
                                .SelectMany(x => x.Errors)
                                .Select(x => x.ErrorMessage));
                return View(model);

            }
            TempData["error"] = "incorrect username or password";
            return View(model);
        }




        #endregion
        //
        // GET: /Account/Login
        [AllowAnonymous]
        
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;
            return View();
        }


        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;

            string recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
            bool isCaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken);
            if (!isCaptchaValid)
            {
                TempData["error"] = "Google reCAPTCHA bot validation failed. Please check the checkbox and try again.";
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                TempData["error"] = "incorrect username or password";
                return View(model);
            }
            //
            //Check if User Mail has been verified
            var user = await UserManager.FindByNameAsync(model.UserName) ?? await UserManager.FindByEmailAsync(model.UserName);
            if (user != null)
            {
                // Check if user is in "Suspended" role ONLY
                bool isSuspended = await UserManager.IsInRoleAsync(user, "Suspended");

                if (isSuspended)
                {
                    return RedirectToAction("Lockout");
                }

                if (user.EmailConfirmed == false)
                {
                    var otpService = HttpContext.RequestServices.GetService<IOtpService>();
                    var zeptoMail = HttpContext.RequestServices.GetService<IZeptoMailService>();

                    if (otpService != null && zeptoMail != null)
                    {
                        var otpCode = otpService.GenerateOtp(user.Email, "email_verification");
                        await zeptoMail.SendEmailVerificationOtpAsync(user.Email, user.UserName, otpCode);
                    }

                    TempData["Success"] = "Please verify your email address to continue. A 6-digit verification code has been dispatched to your email.";
                    return RedirectToAction("VerifyEmail", new { email = user.Email });
                }

                // Check if user has 2FA enabled
                if (user.TwoFactorEnabled && user.PreferredTwoFactorMethod != TwoFactorMethod.None)
                {
                    bool passwordValid = await UserManager.CheckPasswordAsync(user, model.Password);
                    if (!passwordValid)
                    {
                        ModelState.AddModelError("", "Invalid username or password.");
                        return View(model);
                    }

                    // Store pending 2FA session details
                    HttpContext.Session.SetString("Pending2FA_UserId", user.Id);
                    HttpContext.Session.SetString("Pending2FA_RememberMe", model.RememberMe.ToString());
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        HttpContext.Session.SetString("Pending2FA_ReturnUrl", returnUrl);
                    }

                    // Handle OTP dispatch for SMS or Email
                    if (user.PreferredTwoFactorMethod == TwoFactorMethod.SmsOtp)
                    {
                        string otp = _twoFactorService.Generate6DigitOtp();
                        user.LastOtpCode = otp;
                        user.LastOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                        await UserManager.UpdateAsync(user);
                        await _twoFactorService.SendSmsOtpAsync(user.PhoneNumber, otp);
                    }
                    else if (user.PreferredTwoFactorMethod == TwoFactorMethod.EmailOtp)
                    {
                        string otp = _twoFactorService.Generate6DigitOtp();
                        user.LastOtpCode = otp;
                        user.LastOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                        await UserManager.UpdateAsync(user);
                        await _twoFactorService.SendEmailOtpAsync(user.Email, user.UserName, otp);
                    }

                    return RedirectToAction("VerifyTwoFactor");
                }

                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, change lockoutOnFailure: true
                var signInResult = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (signInResult.Succeeded)
                {
                    if (returnUrl != null)
                    {
                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        if (User.IsInRole("Admin"))
                        {
                            return RedirectToAction("Index", "Main", new { @area = "AdminPanel" });
                        }
                        else
                        {
                            return RedirectToAction("Index", "Dashboard", new { @area = "ClientPanel" });
                        }
                    }
                }
                if (signInResult.IsLockedOut)
                    return View("Lockout");
                if (signInResult.RequiresTwoFactor)
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });

                ModelState.AddModelError("", "Invalid login attempt.");
                string messages = string.Join("; ", ModelState.Values
                                .SelectMany(x => x.Errors)
                                .Select(x => x.ErrorMessage));
                return View(model);

            }
            TempData["error"] = "incorrect username or password";
            return View(model);
        }

        #region TWO-FACTOR AUTHENTICATION (2FA) INTERCEPTOR

        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor()
        {
            string userId = HttpContext.Session.GetString("Pending2FA_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            ViewBag.Method = user.PreferredTwoFactorMethod;
            ViewBag.MaskedTarget = user.PreferredTwoFactorMethod == TwoFactorMethod.SmsOtp ? MaskPhone(user.PhoneNumber)
                : user.PreferredTwoFactorMethod == TwoFactorMethod.EmailOtp ? MaskEmail(user.Email)
                : "Authenticator App";

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyTwoFactor(string code)
        {
            string userId = HttpContext.Session.GetString("Pending2FA_UserId");
            string rememberMeStr = HttpContext.Session.GetString("Pending2FA_RememberMe");
            string returnUrl = HttpContext.Session.GetString("Pending2FA_ReturnUrl");
            bool rememberMe = bool.TryParse(rememberMeStr, out bool rm) && rm;

            if (string.IsNullOrEmpty(userId))
            {
                TempData["error"] = "2FA session expired. Please log in again.";
                return RedirectToAction("Login");
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            bool isValid = false;

            if (user.PreferredTwoFactorMethod == TwoFactorMethod.GoogleAuth || user.PreferredTwoFactorMethod == TwoFactorMethod.MicrosoftAuth)
            {
                isValid = _twoFactorService.ValidateTotpCode(user.TwoFactorSecretKey, code);
            }
            else if (user.PreferredTwoFactorMethod == TwoFactorMethod.SmsOtp || user.PreferredTwoFactorMethod == TwoFactorMethod.EmailOtp)
            {
                if (!string.IsNullOrEmpty(user.LastOtpCode) && string.Equals(user.LastOtpCode, code?.Trim(), StringComparison.Ordinal)
                    && user.LastOtpExpiry.HasValue && user.LastOtpExpiry.Value > DateTime.UtcNow)
                {
                    isValid = true;
                    user.LastOtpCode = null; // Clear OTP code after single use
                    await UserManager.UpdateAsync(user);
                }
            }

            if (isValid)
            {
                HttpContext.Session.Remove("Pending2FA_UserId");
                HttpContext.Session.Remove("Pending2FA_RememberMe");
                HttpContext.Session.Remove("Pending2FA_ReturnUrl");

                await SignInManager.SignInAsync(user, isPersistent: rememberMe);

                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return RedirectToLocal(returnUrl);
                }

                var roles = await UserManager.GetRolesAsync(user);
                if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Main", new { area = "AdminPanel" });
                }
                return RedirectToAction("Index", "Dashboard", new { area = "ClientPanel" });
            }

            TempData["error"] = "Invalid verification code. Please check and try again.";
            ViewBag.Method = user.PreferredTwoFactorMethod;
            ViewBag.MaskedTarget = user.PreferredTwoFactorMethod == TwoFactorMethod.SmsOtp ? MaskPhone(user.PhoneNumber)
                : user.PreferredTwoFactorMethod == TwoFactorMethod.EmailOtp ? MaskEmail(user.Email)
                : "Authenticator App";

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResendTwoFactorCode()
        {
            string userId = HttpContext.Session.GetString("Pending2FA_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Session expired." });
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            string otp = _twoFactorService.Generate6DigitOtp();
            user.LastOtpCode = otp;
            user.LastOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await UserManager.UpdateAsync(user);

            if (user.PreferredTwoFactorMethod == TwoFactorMethod.SmsOtp)
            {
                await _twoFactorService.SendSmsOtpAsync(user.PhoneNumber, otp);
            }
            else if (user.PreferredTwoFactorMethod == TwoFactorMethod.EmailOtp)
            {
                await _twoFactorService.SendEmailOtpAsync(user.Email, user.UserName, otp);
            }

            return Json(new { success = true, message = "A new verification code has been dispatched." });
        }

        private string MaskPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 6) return "+234...";
            return phone.Substring(0, 4) + "****" + phone.Substring(phone.Length - 2);
        }

        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@")) return "your email";
            var parts = email.Split('@');
            string name = parts[0];
            string domain = parts[1];
            if (name.Length <= 2) return name[0] + "*@" + domain;
            return name.Substring(0, 2) + "****@" + domain;
        }

        #endregion


        //change password

        public ActionResult ChangePassword()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.userid = id;
            return View();
        }


        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(string userid, string oldPassword, string newPassword)
        {
            var user = await UserManager.FindByIdAsync(userid);
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (user != null)
            {
                var checkpassvalidate = await UserManager.CheckPasswordAsync(user, oldPassword);
                if (checkpassvalidate == true)
                {
                   var removepass = await UserManager.RemovePasswordAsync(user);
                    if (removepass.Succeeded)
                    {
                        var changepass = await UserManager.AddPasswordAsync(user, newPassword);
                        if (changepass.Succeeded)
                        {
                            TempData["success"] = "password change successful";
                            ViewBag.userid = id;
                            return View();
                        }
                    }
                    TempData["error"] = "password change not successful";
                    ViewBag.userid = id;
                    return View();
                }
                TempData["error"] = "Invalid old password";
                ViewBag.userid = id;
                return View();
            }
            TempData["error"] = "Invalid User";
            return View();
        }


        public async Task<ActionResult> ResetUserPassword(string userId)
        {
            var user = await UserManager.FindByIdAsync(userId);
            ViewBag.userid = userId;
            ViewBag.User = user;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetUserPassword(string userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["passworderror"] = "Password must be at least 6 characters.";
                return RedirectToAction("ResetUserPassword", new { userId = userId });
            }

            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["passworderror"] = "User account not found.";
                return RedirectToAction("ResetUserPassword", new { userId = userId });
            }

            if (await UserManager.HasPasswordAsync(user))
            {
                var removeResult = await UserManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded)
                {
                    TempData["passworderror"] = "Error removing previous password: " + string.Join(", ", removeResult.Errors.Select(e => e.Description));
                    return RedirectToAction("ResetUserPassword", new { userId = userId });
                }
            }

            var addResult = await UserManager.AddPasswordAsync(user, newPassword);
            if (addResult.Succeeded)
            {
                await UserManager.UpdateSecurityStampAsync(user);
                TempData["password"] = "Password Changed Successfully.";
                return RedirectToAction("ResetUserPassword", new { userId = userId });
            }

            TempData["passworderror"] = "Unable to change password: " + string.Join(", ", addResult.Errors.Select(e => e.Description));
            return RedirectToAction("ResetUserPassword", new { userId = userId });
        }

        //
        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            // Require that the user has already logged in via username/password or external login
            var user = await SignInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        public async Task<bool> MigrateAccount(string username, string password)
        {
            var decUsername = EncString.Decrypt(username, "ladybee");
            var decPassword = EncString.Decrypt(password, "ladybee");

            //get user from JosUserList
            var usersList = await JosClient.ListJosUser();

            var josUser = usersList.FirstOrDefault(x => x.username == decUsername);

            if (josUser != null)
            {
                var clientList = await JosClient.ListJosClient();

                var josClient = clientList.FirstOrDefault(x => x.ClientId == josUser.id);
                var user = new ApplicationUser { UserName = josUser.username, Email = josUser.email, EmailConfirmed = true };

                //Add Other properties
                user.PhoneNumber = josClient.GSM;
                if (josClient.dob != "0000-00-00")
                {
                    user.DateOfBirth = DateTime.ParseExact(josClient.dob.Replace("-", "/"), "yyyy/MM/dd", CultureInfo.InvariantCulture);
                }
                else
                {
                    user.DateOfBirth = DateTime.UtcNow.Date;
                }
                var result = await UserManager.CreateAsync(user, decPassword);

                if (result.Succeeded)
                {
                    await UserManager.AddToRoleAsync(user, "Client");
                    //await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    //string code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
                    //var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Scheme);
                    //await UserManager.SendEmailAsync(user.Id, "Confirm your account", string.Format("<a href='{0}'>HERE</a>", callbackUrl));

                    return true;
                    //return RedirectToAction("GoToMail");


                }
                AddErrors(result);
            }

            // return View();
            return false;
        }

        //
        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // The following code protects for brute force attacks against the two factor codes.
            // If a user enters incorrect codes for a specified amount of time then the user account
            // will be locked out for a specified amount of time.
            // You can configure the account lockout settings in IdentityConfig
            var result = await SignInManager.TwoFactorAuthenticatorSignInAsync(model.Code, model.RememberMe, model.RememberBrowser);
            if (result.Succeeded)
            {
                return RedirectToLocal(model.ReturnUrl);
            }
            if (result.IsLockedOut)
            {
                return View("Lockout");
            }
            else
            {
                ModelState.AddModelError("", "Invalid code.");
                return View(model);
            }
        }

        //
        // GET: /Account/CheckUsernameAvailability
        [HttpGet]
        [AllowAnonymous]
        public async Task<JsonResult> CheckUsernameAvailability(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            {
                return Json(new { available = false, message = "Username must be at least 3 characters." });
            }

            var user = await UserManager.FindByNameAsync(username.Trim());
            if (user != null)
            {
                return Json(new { available = false, message = "Username is already taken." });
            }

            return Json(new { available = true, message = "Username is available!" });
        }

        //
        // GET: /Account/CheckEmailAvailability
        [HttpGet]
        [AllowAnonymous]
        public async Task<JsonResult> CheckEmailAvailability(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                return Json(new { available = false, message = "Please enter a valid email." });
            }

            var user = await UserManager.FindByEmailAsync(email.Trim());
            if (user != null)
            {
                return Json(new { available = false, message = "An account with this email already exists." });
            }

            return Json(new { available = true, message = "Email is available!" });
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var recaptchaService = HttpContext.RequestServices.GetService<IGoogleReCaptchaService>();
                if (recaptchaService != null)
                {
                    var recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
                    var isHuman = await recaptchaService.VerifyTokenAsync(recaptchaToken);
                    if (!isHuman)
                    {
                        ModelState.AddModelError("", "Please verify the reCAPTCHA box to confirm you are not a robot.");
                        return View(model);
                    }
                }

                var existingUser = await UserManager.FindByNameAsync(model.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "This username is already taken. Please choose another.");
                    return View(model);
                }

                var existingEmail = await UserManager.FindByEmailAsync(model.Email);
                if (existingEmail != null)
                {
                    ModelState.AddModelError("", "An account with this email address already exists.");
                    return View(model);
                }

                var user = new ApplicationUser 
                { 
                    UserName = model.Username.Trim(), 
                    Email = model.Email.Trim(),
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    DateRegitered = DateTime.UtcNow,
                    DateOfBirth = DateTime.UtcNow.Date,
                    EmailConfirmed = false
                };

                var result = await UserManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await UserManager.AddToRoleAsync(user, "Client");

                    var otpService = HttpContext.RequestServices.GetService<IOtpService>();
                    var zeptoMail = HttpContext.RequestServices.GetService<IZeptoMailService>();

                    if (otpService != null && zeptoMail != null)
                    {
                        var otpCode = otpService.GenerateOtp(user.Email, "email_verification");
                        await zeptoMail.SendEmailVerificationOtpAsync(user.Email, user.UserName, otpCode);
                    }

                    TempData["Success"] = "Your account has been created! Enter the 6-digit verification code sent to your email.";
                    return RedirectToAction("VerifyEmail", new { email = user.Email });
                }
                AddErrors(result);
            }

            return View(model);
        }

        //
        // GET: /Account/VerifyEmail
        [AllowAnonymous]
        public ActionResult VerifyEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("Register");
            }
            return View(new VerifyEmailViewModel { Email = email });
        }

        //
        // POST: /Account/VerifyEmail
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Unable to locate account. Please register again.");
                return View(model);
            }

            if (user.EmailConfirmed)
            {
                TempData["Success"] = "Your email is already verified! Please sign in.";
                return RedirectToAction("Login");
            }

            var otpService = HttpContext.RequestServices.GetService<IOtpService>();
            bool isValid = otpService != null && otpService.ValidateOtp(model.Email, "email_verification", model.OtpCode);

            if (!isValid)
            {
                ModelState.AddModelError("", "Invalid or expired 6-digit verification code. Please check your inbox or click Resend.");
                return View(model);
            }

            // Confirm Email
            user.EmailConfirmed = true;
            await UserManager.UpdateAsync(user);

            // Credit 20 Free Units upon successful email verification
            var db = HttpContext.RequestServices.GetService<ApplicationDbContext>();
            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            decimal freeUnits = adminSetting != null && adminSetting.UnitPerNewMember > 0 ? adminSetting.UnitPerNewMember : 20;

            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (client == null)
            {
                client = new Client
                {
                    UserId = user.Id,
                    Units = freeUnits,
                    FirstName = user.UserName,
                    Discount = 0,
                    AllowNotifications = AllowNotifications.Allow
                };
                db.Clients.Add(client);
                await db.SaveChangesAsync();
            }
            else
            {
                client.Units = (client.Units <= 0) ? freeUnits : (client.Units + freeUnits);
                await db.SaveChangesAsync();
            }

            // Create Free Welcome Bonus Transaction Entry
            try
            {
                var bonusTx = new Transaction
                {
                    UserId = user.Id,
                    ClientId = client.ClientId,
                    Amount = 0,
                    AmountPaid = 0,
                    Units = freeUnits,
                    TransactionType = TransactionType.ByAdmin,
                    Status = TransactionStatus.Approved,
                    DateCreated = DateTime.UtcNow,
                    DateApproved = DateTime.UtcNow,
                    ApprovedBy = "System (Welcome Bonus)",
                    Note = $"Welcome credit of {freeUnits:N0} free test units upon email verification",
                    TransactionReference = "BONUS-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                };
                db.Transactions.Add(bonusTx);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bonus Transaction Error]: {ex.Message}");
            }

            // Send Welcome Email
            var zeptoMail = HttpContext.RequestServices.GetService<IZeptoMailService>();
            if (zeptoMail != null)
            {
                await zeptoMail.SendWelcomeEmailAsync(user.Email, user.UserName, freeUnits);
            }

            // Automatically sign in the user
            await SignInManager.SignInAsync(user, isPersistent: false);

            TempData["Success"] = $"Account verified! {freeUnits:N0} Free Test Messaging Units have been credited to your wallet.";
            return RedirectToAction("Index", "Dashboard", new { area = "ClientPanel" });
        }

        //
        // GET/POST: /Account/ResendVerificationOtp
        [AllowAnonymous]
        public async Task<ActionResult> ResendVerificationOtp(string email)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true;

            if (string.IsNullOrWhiteSpace(email))
            {
                if (isAjax) return Json(new { success = false, message = "Email is required." });
                return RedirectToAction("Register");
            }

            var user = await UserManager.FindByEmailAsync(email);
            if (user != null && !user.EmailConfirmed)
            {
                var otpService = HttpContext.RequestServices.GetService<IOtpService>();
                var zeptoMail = HttpContext.RequestServices.GetService<IZeptoMailService>();

                if (otpService != null && zeptoMail != null)
                {
                    var otpCode = otpService.GenerateOtp(user.Email, "email_verification");
                    await zeptoMail.SendEmailVerificationOtpAsync(user.Email, user.UserName, otpCode);
                }

                if (isAjax) return Json(new { success = true, message = "A fresh 6-digit verification code has been dispatched to your email." });
                TempData["Success"] = "A fresh 6-digit verification code has been dispatched to your email.";
                return RedirectToAction("VerifyEmail", new { email = email });
            }
            else if (user != null && user.EmailConfirmed)
            {
                if (isAjax) return Json(new { success = false, message = "Your email is already verified! Please sign in." });
                TempData["Success"] = "Your email is already verified! Please sign in.";
                return RedirectToAction("Login");
            }

            if (isAjax) return Json(new { success = false, message = "No account found with this email address." });
            TempData["error"] = "No account found with this email address.";
            return RedirectToAction("Register");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;

            string recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
            if (!string.IsNullOrWhiteSpace(recaptchaToken))
            {
                bool isCaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken);
                if (!isCaptchaValid)
                {
                    TempData["error"] = "Security bot validation failed. Please try again.";
                    return View(model);
                }
            }

            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByEmailAsync(model.Email) ?? await UserManager.FindByNameAsync(model.Email);
                if (user != null)
                {
                    var otpService = HttpContext.RequestServices.GetService<IOtpService>() ?? new OtpService();
                    var zeptoMail = HttpContext.RequestServices.GetService<IZeptoMailService>() ?? new ZeptoMailService();

                    var otpCode = otpService.GenerateOtp(user.Email, "password_reset");
                    bool isSent = await zeptoMail.SendPasswordResetOtpAsync(user.Email, user.UserName, otpCode);

                    if (!isSent)
                    {
                        TempData["error"] = "We could not deliver the recovery code to your email at this time. Please verify your email address or contact support.";
                        return View(model);
                    }

                    TempData["Success"] = "A 6-digit password reset code has been sent to your email.";
                    return RedirectToAction("ResetPasswordOtp", new { email = user.Email });
                }

                ModelState.AddModelError("Email", "No account found matching this email or username. Please check your spelling.");
                return View(model);
            }

            return View(model);
        }

        //
        // GET/POST: /Account/ResendPasswordResetOtp
        [AllowAnonymous]
        public async Task<ActionResult> ResendPasswordResetOtp(string email)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true;

            if (string.IsNullOrWhiteSpace(email))
            {
                if (isAjax) return Json(new { success = false, message = "Email is required." });
                return RedirectToAction("ForgotPassword");
            }

            var user = await UserManager.FindByEmailAsync(email) ?? await UserManager.FindByNameAsync(email);
            if (user != null)
            {
                var otpService = HttpContext.RequestServices.GetService<IOtpService>() ?? new OtpService();
                var zeptoMail = HttpContext.RequestServices.GetService<IZeptoMailService>() ?? new ZeptoMailService();

                var otpCode = otpService.GenerateOtp(user.Email, "password_reset");
                bool isSent = await zeptoMail.SendPasswordResetOtpAsync(user.Email, user.UserName, otpCode);

                if (isSent)
                {
                    if (isAjax) return Json(new { success = true, message = "A fresh 6-digit password reset code has been dispatched to your email." });
                    TempData["Success"] = "A fresh 6-digit password reset code has been dispatched to your email.";
                    return RedirectToAction("ResetPasswordOtp", new { email = email });
                }
                else
                {
                    if (isAjax) return Json(new { success = false, message = "Unable to dispatch email. Please check your email address or try again shortly." });
                    TempData["error"] = "Unable to dispatch email. Please try again shortly.";
                    return RedirectToAction("ResetPasswordOtp", new { email = email });
                }
            }

            if (isAjax) return Json(new { success = false, message = "No account found matching this email address." });
            TempData["error"] = "No account found with this email address. Please enter your registered email.";
            return RedirectToAction("ForgotPassword");
        }

        //
        // GET: /Account/ResetPasswordOtp
        [AllowAnonymous]
        public ActionResult ResetPasswordOtp(string email)
        {
            return View(new ResetPasswordOtpViewModel { Email = email });
        }

        //
        // POST: /Account/ResetPasswordOtp
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPasswordOtp(ResetPasswordOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "No account found with this email address.");
                return View(model);
            }

            var otpService = HttpContext.RequestServices.GetService<IOtpService>();
            bool isValid = otpService != null && otpService.ValidateOtp(model.Email, "password_reset", model.OtpCode);

            if (!isValid)
            {
                ModelState.AddModelError("", "Invalid or expired reset code. Please request a new code.");
                return View(model);
            }

            var resetToken = await UserManager.GeneratePasswordResetTokenAsync(user);
            var result = await UserManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Your password has been reset successfully! Please sign in with your new password.";
                return RedirectToAction("Login");
            }

            AddErrors(result);
            return View(model);
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider
            return Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }) }, provider);
        }

        //
        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var user = await SignInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(user);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // Generate the token and send it
            var user = await SignInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            // generate token (sending SMS/email not implemented here)
            var token = await UserManager.GenerateTwoFactorTokenAsync(user, model.SelectedProvider);
            // TODO: send token via appropriate provider
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await SignInManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, isPersistent: false, bypassTwoFactor: false);
            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return View("Lockout");
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
            }

            // If the user does not have an account, then prompt the user to create an account
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.LoginProvider = loginInfo.LoginProvider;
            return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Principal?.FindFirstValue(ClaimTypes.Email) });
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Manage");
            }

                if (ModelState.IsValid)
                {
                    // Get the information about the user from the external login provider
                    var info = await SignInManager.GetExternalLoginInfoAsync();
                    if (info == null)
                    {
                        return View("ExternalLoginFailure");
                    }
                    var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                    var result = await UserManager.CreateAsync(user);
                    if (result.Succeeded)
                    {
                        var login = new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName);
                        result = await UserManager.AddLoginAsync(user, login);
                        if (result.Succeeded)
                        {
                            await SignInManager.SignInAsync(user, isPersistent: false);
                            return RedirectToLocal(returnUrl);
                        }
                    }
                    AddErrors(result);
                }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> LogOff()
        {
            await SignInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _userManager = null;
                _signInManager = null;
            }

            base.Dispose(disposing);
        }

        #region LOCKOUT EMERGENCY APPEAL TICKET

        [AllowAnonymous]
        public IActionResult Lockout()
        {
            ViewBag.ReCaptchaSiteKey = new GoogleReCaptchaService().SiteKey;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitLockoutTicket(string senderName, string senderEmail, string senderPhone, string message, string gReCaptchaResponse)
        {
            string recaptchaToken = !string.IsNullOrWhiteSpace(gReCaptchaResponse) ? gReCaptchaResponse : Request.Form["g-recaptcha-response"].ToString();
            bool isCaptchaValid = await new GoogleReCaptchaService().VerifyTokenAsync(recaptchaToken);
            if (!isCaptchaValid)
            {
                TempData["error"] = "Google reCAPTCHA bot validation failed. Please check the security checkbox and try again.";
                return RedirectToAction("Lockout");
            }

            if (string.IsNullOrWhiteSpace(senderName) || string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(message))
            {
                TempData["error"] = "Name, email, and detailed message are required.";
                return RedirectToAction("Lockout");
            }

            var ticketService = new TicketService();
            var ticket = new SupportTicket
            {
                SenderName = senderName.Trim(),
                SenderEmail = senderEmail.Trim(),
                SenderPhone = senderPhone?.Trim(),
                Subject = "Account Restriction Appeal / Unblock Request",
                Message = message.Trim(),
                Category = TicketCategory.AccountSuspended,
                Priority = TicketPriority.Urgent
            };

            await ticketService.CreateTicketAsync(ticket);
            TempData["success"] = $"Emergency appeal ticket #{ticket.TicketNumber} submitted! Support notification email has been dispatched.";

            return RedirectToAction("Lockout");
        }

        #endregion

        #region Helpers

        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private object AuthenticationManager
        {
            get { return null; }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description ?? error.Code);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        



        #endregion Helpers
    }
}











