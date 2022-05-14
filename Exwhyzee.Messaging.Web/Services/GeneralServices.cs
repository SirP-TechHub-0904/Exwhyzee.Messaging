using Exwhyzee.Messaging.Web.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Exwhyzee.Messaging.Web.Services
{
    public class GeneralServices
    {
        public static bool IsUserInRole(string userId, string role)
        {
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(new ApplicationDbContext()));
            var manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext()));

            if (manager.IsInRole(userId, role))
            {
                return true;
            }

            return false;
        }

        public static string ClientName(int id)
        {
            var name = "";
            using (var db = new ApplicationDbContext())
            {
                var client = db.Clients.Include(x => x.User).FirstOrDefault(x => x.ClientId == id);
                if (client != null)
                {
                    name = client.User.UserName;
                }
            };

            return name;
        }

        public static string UserName(string id)
        {
            var name = "";
            using (var db = new ApplicationDbContext())
            {
                var client = db.Users.FirstOrDefault(x => x.Id == id);
                if (client != null)
                {
                    name = client.UserName;
                }
            };

            return name;
        }

        public static int PendingTransactions()
        {
            int count = 0;
            using (var db = new ApplicationDbContext())
            {
                var trans = db.Transactions.Where(x => x.Status == TransactionStatus.Pending);
                count = trans.Count();
            }

            return count;
        }
        public static int NumberCount(string Numbers)
        {
            string n = Numbers.Replace("\r\n", ",");
            IList<string> numbers = n.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            numbers = numbers.Distinct().ToList();
            return numbers.Count();
        }
        public static decimal GetUnits(string id)
        {
            decimal units = 0;

            using (var db = new ApplicationDbContext())
            {
                var trans = db.Clients.FirstOrDefault(x => x.UserId == id);
                units = trans.Units;
            }

            return units;
        }
    }
}