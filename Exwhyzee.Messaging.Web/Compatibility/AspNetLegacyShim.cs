using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using System.Linq.Expressions;

namespace Exwhyzee.Messaging.Web.Compatibility
{
    // Minimal shim for ConfigurationManager.AppSettings usage
    public static class ConfigurationManager
    {
        private static readonly NameValueCollection _appSettings = new NameValueCollection();
        public static NameValueCollection AppSettings => _appSettings;
    }

    // HttpRequest compatibility: CreateResponse -> ObjectResult
    public static class HttpRequestExtensions
    {
        public static ObjectResult CreateResponse(this HttpRequest req, HttpStatusCode status, object content)
        {
            var result = new ObjectResult(content)
            {
                StatusCode = (int)status
            };
            return result;
        }

        public static bool IsAuthenticated(this HttpRequest req)
        {
            return req?.HttpContext?.User?.Identity?.IsAuthenticated == true;
        }
    }

    // Identity extensions for views
    public static class IdentityShim
    {
        public static string GetUserId(this IIdentity identity)
        {
            if (identity is ClaimsIdentity ci)
            {
                return ci.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
            return null;
        }

        public static string GetUserName(this IIdentity identity)
        {
            if (identity is ClaimsIdentity ci)
            {
                return ci.FindFirst(ClaimTypes.Name)?.Value ?? ci.Name;
            }
            return identity?.Name;
        }
    }

    // HtmlHelper compatibility shims
    public static class HtmlHelperCompat
    {
        // Label overloads that existed in older MVC
        public static Microsoft.AspNetCore.Html.IHtmlContent Label(this IHtmlHelper html, string labelText, object htmlAttributes)
        {
            return html.Label(labelText, labelText, htmlAttributes);
        }

        // BeginForm overload to accept common parameter list without the nullable bool
        public static IDisposable BeginForm(this IHtmlHelper html, string action, string controller, object routeValues, FormMethod method, object htmlAttributes)
        {
            return html.BeginForm(action, controller, routeValues, method, null, htmlAttributes);
        }

        // EnumDropDownListFor simple implementation
        public static Microsoft.AspNetCore.Html.IHtmlContent EnumDropDownListFor<TModel, TEnum>(this IHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, object htmlAttributes)
        {
            var enumType = typeof(TEnum);
            var values = Enum.GetValues(enumType).Cast<object>().Select(v => new SelectListItem { Value = v.ToString(), Text = v.ToString() }).ToList();
            var name = GetExpressionText(expression);
            return html.DropDownList(name, new SelectList(values, "Value", "Text"), htmlAttributes);
        }

        private static string GetExpressionText<TModel, TEnum>(Expression<Func<TModel, TEnum>> expression)
        {
            if (expression.Body is MemberExpression me)
                return me.Member.Name;
            if (expression.Body is UnaryExpression ue && ue.Operand is MemberExpression me2)
                return me2.Member.Name;
            return expression.ToString();
        }
    }

    // Simple AuthenticationDescription shim to satisfy older views
    public class AuthenticationDescription
    {
        public string AuthenticationType { get; set; }
        public string Caption { get; set; }
    }

    // Simple PagedList shim
    public class StaticPagedList<T> : List<T>
    {
        public int PageNumber { get; }
        public int PageSize { get; }
        public int TotalItemCount { get; }

        public StaticPagedList(IEnumerable<T> subset, int pageNumber, int pageSize, int totalItemCount) : base(subset)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalItemCount = totalItemCount;
        }
    }

    public static class PagingExtensions
    {
        public static StaticPagedList<T> ToPagedListCompat<T>(this IEnumerable<T> source, int pageNumber, int pageSize)
        {
            var total = source.Count();
            var subset = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new StaticPagedList<T>(subset, pageNumber, pageSize, total);
        }
    }

    // SignInStatus and compatibility for PasswordSignInAsync
    public enum SignInStatus
    {
        Success,
        LockedOut,
        RequiresVerification,
        Failure
    }

    public static class SignInManagerExtensions
    {
        public static async Task<SignInStatus> PasswordSignInAsync(this SignInManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> manager, string userName, string password, bool rememberMe, bool shouldLockout)
        {
            var res = await manager.PasswordSignInAsync(userName, password, rememberMe, shouldLockout);
            if (res.Succeeded) return SignInStatus.Success;
            if (res.IsLockedOut) return SignInStatus.LockedOut;
            if (res.RequiresTwoFactor) return SignInStatus.RequiresVerification;
            return SignInStatus.Failure;
        }

        public static async Task<bool> HasBeenVerifiedAsync(this SignInManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> manager)
        {
            // There is no direct replacement; assume false to allow existing code to run
            return await Task.FromResult(false);
        }

        public static AuthenticationProperties ConfigureExternalAuthenticationProperties(this SignInManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> manager, string provider, string redirectUrl, string userId = null)
        {
            var props = manager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            if (!string.IsNullOrEmpty(userId))
            {
                props.Items["XsrfId"] = userId;
            }
            return props;
        }
    }

    // UserManager compatibility extensions for methods that previously accepted string ids
    public static class UserManagerCompat
    {
        public static async Task<string> GenerateEmailConfirmationTokenAsync(this UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> um, string userId)
        {
            var user = await um.FindByIdAsync(userId);
            return await um.GenerateEmailConfirmationTokenAsync(user);
        }

        public static async Task<string> GeneratePasswordResetTokenAsync(this UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> um, string userId)
        {
            var user = await um.FindByIdAsync(userId);
            return await um.GeneratePasswordResetTokenAsync(user);
        }

        public static IdentityResult RemovePassword(this UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> um, string userId)
        {
            var user = um.FindByIdAsync(userId).GetAwaiter().GetResult();
            return um.RemovePasswordAsync(user).GetAwaiter().GetResult();
        }

        public static IdentityResult AddPassword(this UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> um, string userId, string newPassword)
        {
            var user = um.FindByIdAsync(userId).GetAwaiter().GetResult();
            return um.AddPasswordAsync(user, newPassword).GetAwaiter().GetResult();
        }

        public static async Task<string> GenerateEmailConfirmationTokenAsync(this UserManager<Exwhyzee.Messaging.Core.Models.ApplicationUser> um, Exwhyzee.Messaging.Core.Models.ApplicationUser user)
        {
            return await um.GenerateEmailConfirmationTokenAsync(user);
        }
    }
}
