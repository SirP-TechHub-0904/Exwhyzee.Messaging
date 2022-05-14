using Exwhyzee.Messaging.Web.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Exwhyzee.Messaging.Web.Services
{
    public static class JosClient
    {
        public static async Task<List<JosUser>> ListJosUser()
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Data/jos_users.json")))
            {
                string json = sr.ReadToEnd();
                List<JosUser> users = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<JosUser>>(json));

                return users;
            }
        }

        public static async Task<List<JosSpcClient>> ListJosClient()
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Data/jos_spcClient.json")))
            {
                string json = sr.ReadToEnd();
                List<JosSpcClient> client = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<JosSpcClient>>(json));

                return client;
            }
        }


        public static async Task<List<JosSpcMessage>> ListJosMessage()
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Data/jos_spcMessages.json")))
            {
                string json = sr.ReadToEnd();
                List<JosSpcMessage> messages = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<JosSpcMessage>>(json));

                return messages;
            }
        }

        public static async Task<List<josSpcDraft>> ListJosDraftMessage()
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Data/jos_spcDraft.json")))
            {
                string json = sr.ReadToEnd();
                List<josSpcDraft> draft = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<josSpcDraft>>(json));

                return draft;
            }
        }


        public static async Task<List<josSpcGroups>> ListJosGroup()
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Data/jos_spcGroups.json")))
            {
                string json = sr.ReadToEnd();
                List<josSpcGroups> group = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<josSpcGroups>>(json));

                return group;
            }
        }


        public static async Task<List<josSpcAddressBook>> ListJosGroupContact()
        {
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/Data/jos_spcAddressBook.json")))
            {
                string json = sr.ReadToEnd();
                List<josSpcAddressBook> contact = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<List<josSpcAddressBook>>(json));

                return contact;
            }
        }


        public static async Task<JosUser> GetJosUser(string username)
        {
            if (!string.IsNullOrEmpty(username))
            {
                var userList = await ListJosUser();

                var user = userList.FirstOrDefault(x => x.username == username);

                return user;
            }

            return null;
        }

        public static async Task<JosSpcClient> GetJosClient(string username)
        {
            if (!string.IsNullOrEmpty(username))
            {
                var userList = await ListJosUser();

                var user = userList.FirstOrDefault(x => x.username == username);
                var clientList = await ListJosClient();

                var client = clientList.FirstOrDefault(x => x.ClientId == user.id);


                return client;
            }

            return null;
        }

        public static async Task<JosSpcClient> GetJosClientById(int clientId)
        {
            var clientList = await ListJosClient();

            var client = clientList.FirstOrDefault(x => x.ClientId == clientId);
        //    string josUsrs = "";
        //    var contact = clientList.Select(x => x.GSM);


        
        //var c = contact.Count();
        //    foreach (var a in contact)
        //    {
        //    if(a != null)
        //    {
        //        josUsrs = josUsrs + "," + a;
        //    }
        //    }

        //    var b = josUsrs;
            if (client != null)
            {
                return client;
            }

            return null;
        }

        public static async Task<bool> CheckUserExistInJosUser(string username, string password)
        {
            var user = await ListJosUser();

            var msg = await ListJosMessage();

            var client = user.FirstOrDefault(x => x.username == username);

            if (client != null)
            {
                string[] arr = client.password
            .Split(":".ToCharArray(), 2, StringSplitOptions.RemoveEmptyEntries);

                if (arr.Length == 2)
                {
                    // new format as {HASH}:{SALT}
                    string cryptpass = arr[0];
                    string salt = arr[1];

                    return (CreateMd5Hash(password + salt).Equals(cryptpass));
                }
                else
                {
                    // old format as {HASH} just like PHPbb and many other apps
                    string cryptpass = client.password;

                    return (CreateMd5Hash(password).Equals(cryptpass));
                }
            }

            return false;
        }

        private static string CreateMd5Hash(String data)
        {
            byte[] bdata = new byte[data.Length];
            byte[] hash;

            for (int i = 0; i < data.Length; i++)
            {
                bdata[i] = (byte)(data[i] & 0xff);
            }

            try
            {
                MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider();
                hash = md5Provider.ComputeHash(bdata);
            }
            catch (SecurityException e)
            {
                throw new ApplicationException("A security encryption error occured.", e);
            }

            var result = new StringBuilder(32);

            foreach (byte t in hash)
            {
                String x = (t & 0xff).ToString("X").ToLowerInvariant();

                if (x.Length < 2)
                {
                    result.Append("0");
                }
                result.Append(x);
            }

            return result.ToString();
        }
    }
}