using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Services;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Recaptcha.Web;
using Recaptcha.Web.Mvc;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;

namespace Exwhyzee.Messaging.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationRoleManager _roleManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager, ApplicationRoleManager roleManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
            RoleManager = roleManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
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
                return _roleManager ?? HttpContext.GetOwinContext().Get<ApplicationRoleManager>();
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
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        #region
        //
        // GET: /Account/Login
        [AllowAnonymous]
        [OutputCache(NoStore = true, Location = OutputCacheLocation.None)]
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
                        var result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);
                        switch (result)
                        {
                            case SignInStatus.Success:
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

                            case SignInStatus.LockedOut:
                                return View("Lockout");

                            case SignInStatus.RequiresVerification:
                                return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });

                            case SignInStatus.Failure:
                            default:
                                ModelState.AddModelError("", "Invalid login attempt.");
                                string messages = string.Join("; ", ModelState.Values
                                                .SelectMany(x => x.Errors)
                                                .Select(x => x.ErrorMessage));
                                return View(model);
                        }

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
                // To enable password failures to trigger account lockout, change to shouldLockout: true
                var result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);
                switch (result)
                {
                    case SignInStatus.Success:
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

                    case SignInStatus.LockedOut:
                        return View("Lockout");

                    case SignInStatus.RequiresVerification:
                        return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });

                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Invalid login attempt.");
                        string messages = string.Join("; ", ModelState.Values
                                        .SelectMany(x => x.Errors)
                                        .Select(x => x.ErrorMessage));
                        return View(model);
                }

            }
            TempData["error"] = "incorrect username or password";
            return View(model);
        }




        #endregion
        //
        // GET: /Account/Login
        [AllowAnonymous]
        [OutputCache(NoStore = true, Location = OutputCacheLocation.None)]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }


        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
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

            }
            else
            {
                if (user.EmailConfirmed == false)
                {
                    return RedirectToAction("GoToMail", new { id = user.Id });
                }

                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, change to shouldLockout: true
                var result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);
                switch (result)
                {
                    case SignInStatus.Success:
                        
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

                    case SignInStatus.LockedOut:
                        return View("Lockout");

                    case SignInStatus.RequiresVerification:
                        return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });

                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Invalid login attempt.");
                        string messages = string.Join("; ", ModelState.Values
                                        .SelectMany(x => x.Errors)
                                        .Select(x => x.ErrorMessage));
                        return View(model);
                }

            }
            TempData["error"] = "incorrect username or password";
            return View(model);
        }


        //change password

        public ActionResult ChangePassword()
        {
            var id = User.Identity.GetUserId();
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
            var id = User.Identity.GetUserId();
            if (user != null)
            {
                var checkpassvalidate = await UserManager.CheckPasswordAsync(user, oldPassword);
                if (checkpassvalidate == true)
                {
                   var removepass = await UserManager.RemovePasswordAsync(user.Id);
                    if (removepass.Succeeded)
                    {
                        var changepass = await UserManager.AddPasswordAsync(user.Id, newPassword);
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
            ViewBag.userid = userId;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetUserPassword(string userId, string newPassword)
        {

            //await  _userManager.RemovePasswordAsync(userId);

            // await  _userManager.AddPasswordAsync(userId, newPassword);
            var removePassword = UserManager.RemovePassword(userId);
            if (removePassword.Succeeded)
            {
                //Removed Password Success
                var AddPassword = UserManager.AddPassword(userId, newPassword);
                if (AddPassword.Succeeded)
                {
                    //var userm = _userManager.Users.FirstOrDefault(x => x.Id == userId);
                    TempData["password"] = "Password Changed Successful.";
                    return RedirectToAction("ResetUserPassword", new { userId = userId });
                }
            }

            TempData["passworderror"] = "Unable To Changed Password.";
            return RedirectToAction("ResetUserPassword", new { userId = userId });
        }

        //
        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            // Require that the user has already logged in via username/password or external login
            if (!await SignInManager.HasBeenVerifiedAsync())
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
                    await UserManager.AddToRoleAsync(user.Id, "Client");
                    //await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    //string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    //var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
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
            var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent: model.RememberMe, rememberBrowser: model.RememberBrowser);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(model.ReturnUrl);

                case SignInStatus.LockedOut:
                    return View("Lockout");

                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid code.");
                    return View(model);
            }
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
                RecaptchaVerificationHelper recaptchaHelper = this.GetRecaptchaVerificationHelper();
                if (string.IsNullOrEmpty(recaptchaHelper.Response))
                {
                    ModelState.AddModelError("", "Captcha answer cannot be empty.");
                    return View(model);
                }
                RecaptchaVerificationResult recaptchaResult = await recaptchaHelper.VerifyRecaptchaResponseTaskAsync();
                if (recaptchaResult != RecaptchaVerificationResult.Success)
                {
                    ModelState.AddModelError("", "Incorrect captcha answer.");
                    return View(model);
                }
                var user = new ApplicationUser { UserName = model.Username, Email = model.Email };

                //Add Other properties
                user.PhoneNumber = model.PhoneNumber;
                user.DateRegitered = DateTime.UtcNow.AddHours(1);
                user.DateOfBirth = DateTime.ParseExact(model.DateOfBirth.ToString(), "MM/dd/yyyy", null);
                var result = await UserManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await UserManager.AddToRoleAsync(user.Id, "Client");
                    //await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    await UserManager.SendEmailAsync(user.Id, "Confirm your account", string.Format("<a href='{0}'>HERE</a>", callbackUrl));
                    return RedirectToAction("GoToMail", new { id = user.Id });
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [AllowAnonymous]
        public async Task<ActionResult> ResendActivationCode(string id)
        {
            string code = await UserManager.GenerateEmailConfirmationTokenAsync(id);
            var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = id, code = code }, protocol: Request.Url.Scheme);
            await UserManager.SendEmailAsync(id, "Confirm your account", string.Format("<a href='{0}'>HERE</a>", callbackUrl));
            TempData["success"] = "Verification Mail Resent, check your inbox or spam folder";
            return RedirectToAction("GoToMail", new { id = id });
        }
        //goto email view
        [AllowAnonymous]
        public ActionResult GoToMail(string id)
        {
            ViewBag.id = id;
            return View();
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(model.Email);
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return View("ForgotPasswordConfirmation");
                }

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                // Send an email with this link
                string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                await UserManager.SendEmailAsync(user.Id, "Reset Password", "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>");

                return RedirectToAction("ForgotPasswordConfirmation", "Account");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
            return View();
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
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
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
            if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
            {
                return View("Error");
            }
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);

                case SignInStatus.LockedOut:
                    return View("Lockout");

                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });

                case SignInStatus.Failure:
                default:
                    // If the user does not have an account, then prompt the user to create an account
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
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
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
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
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
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
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers

        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
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

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        #endregion Helpers
    }
}