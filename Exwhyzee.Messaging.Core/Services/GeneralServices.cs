using Exwhyzee.Messaging.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Exwhyzee.Messaging.Core.Services
{
    public class GeneralServices
    {
                public static bool IsUserInRole(string userId, string role)
        {
            using (var db = new ApplicationDbContext())
            {
                var roleEntity = db.Roles.FirstOrDefault(r => r.Name == role);
                if (roleEntity != null)
                {
                    return db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == roleEntity.Id);
                }
                return false;
            }
        }public static string ClientName(int id)
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
