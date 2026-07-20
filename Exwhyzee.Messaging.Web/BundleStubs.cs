using Microsoft.AspNetCore.Html;
using System.Text;

namespace Exwhyzee.Messaging.Web
{
    public static class Styles
    {
        public static IHtmlContent Render(params string[] paths)
        {
            var sb = new StringBuilder();
            foreach (var path in paths)
            {
                if (path == "~/Admin/css")
                {
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/bootstrap/css/bootstrap.min.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/font-awesome/css/font-awesome.min.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/ionicons/css/ionicons.min.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/dist/css/AdminLTE.min.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/dist/css/site.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/dist/css/skins/_all-skins.min.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/plugins/iCheck/flat/blue.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/plugins/datepicker/datepicker3.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/Dashboard/plugins/bootstrap-wysihtml5/bootstrap3-wysihtml5.min.css\" />");
                }
                else if (path == "~/Content/css")
                {
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/bootstrap.css\" />");
                    sb.AppendLine("<link rel=\"stylesheet\" href=\"/Content/site.css\" />");
                }
                else
                {
                    var resolvedPath = path.Replace("~/", "/");
                    sb.AppendLine($"<link rel=\"stylesheet\" href=\"{resolvedPath}\" />");
                }
            }
            return new HtmlString(sb.ToString());
        }
    }

    public static class Scripts
    {
        public static IHtmlContent Render(params string[] paths)
        {
            var sb = new StringBuilder();
            foreach (var path in paths)
            {
                if (path == "~/bundles/adminjs")
                {
                    sb.AppendLine("<script src=\"/Content/Dashboard/plugins/jQuery/jquery-2.2.3.min.js\"></script>");
                    sb.AppendLine("<script src=\"/Content/Dashboard/plugins/jQuery/jquery-ui-1.12.0.min.js\"></script>");
                    sb.AppendLine("<script src=\"/Content/Dashboard/bootstrap/js/bootstrap.min.js\"></script>");
                    sb.AppendLine("<script src=\"/Content/Dashboard/plugins/datepicker/bootstrap-datepicker.js\"></script>");
                    sb.AppendLine("<script src=\"/Content/Dashboard/dist/js/app.min.js\"></script>");
                }
                else if (path == "~/bundles/jquery")
                {
                    sb.AppendLine("<script src=\"/Scripts/jquery-1.10.2.js\"></script>");
                }
                else if (path == "~/bundles/jqueryval")
                {
                    sb.AppendLine("<script src=\"/Scripts/jquery.validate.js\"></script>");
                    sb.AppendLine("<script src=\"/Scripts/jquery.validate.unobtrusive.js\"></script>");
                }
                else if (path == "~/bundles/modernizr")
                {
                    sb.AppendLine("<script src=\"/Scripts/modernizr-2.6.2.js\"></script>");
                }
                else if (path == "~/bundles/bootstrap")
                {
                    sb.AppendLine("<script src=\"/Scripts/bootstrap.js\"></script>");
                    sb.AppendLine("<script src=\"/Scripts/respond.js\"></script>");
                }
                else
                {
                    var resolvedPath = path.Replace("~/", "/");
                    sb.AppendLine($"<script src=\"{resolvedPath}\"></script>");
                }
            }
            return new HtmlString(sb.ToString());
        }
    }
}
