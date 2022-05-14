using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class JosUser
    {
        public int id { get; set; }
        public string name { get; set; }
        public string username { get; set; }
        public string email { get; set; }
        public string password { get; set; }
        public string usertype { get; set; }
        public int? block { get; set; }
        public int? sendEmail { get; set; }
        public int? gid { get; set; }
        public string registerDate { get; set; }
        public string lastvisitDate { get; set; }
        public string activation { get; set; }
        public string param { get; set; }
    }
}