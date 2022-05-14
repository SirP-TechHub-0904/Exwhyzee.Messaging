using System.Web;
using System.Web.Optimization;

namespace Exwhyzee.Messaging.Web
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/respond.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/site.css"));

            bundles.Add(new StyleBundle("~/Admin/css").Include(
                      "~/Content/Dashboard/bootstrap/css/bootstrap.min.css",
                      "~/Content/Dashboard/font-awesome/css/font-awesome.min.css",
                      "~/Content/Dashboard/ionicons/css/ionicons.min.css",
                      "~/Content/Dashboard/dist/css/AdminLTE.min.css",
                      "~/Content/Dashboard/dist/css/site.css",
                      "~/Content/Dashboard/dist/css/skins/_all-skins.min.css",
                       "~/Content/Dashboard/plugins/iCheck/flat/blue.css",
                        "~/Content/Dashboard/plugins/datepicker/datepicker3.css",
                         "~/Content/Dashboard/plugins/bootstrap-wysihtml5/bootstrap3-wysihtml5.min.css"

                      ));

            bundles.Add(new ScriptBundle("~/bundles/adminjs").Include(
                        "~/Content/Dashboard/plugins/jQuery/jquery-2.2.3.min.js",
                        "~/Content/Dashboard/plugins/jQuery/jquery-ui-1.12.0.min.js",
                        "~/Content/Dashboard/bootstrap/js/bootstrap.min.js",
                        "~/Content/Dashboard/plugins/datepicker/bootstrap-datepicker.js",
                        "~/Content/Dashboard/dist/js/app.min.js"

                        ));
        }
    }
}
