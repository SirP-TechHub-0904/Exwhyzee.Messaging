using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Exwhyzee.Messaging.Web.Services
{
    public class SmsServices
    {
        public static bool HasBlackListedWords(string input, string blackLists)
        {
            Regex wordFilter = new Regex(blackLists);
            return wordFilter.IsMatch(input);
        }

        public static List<string> RemoveDuplicates(string input)
        {
            //return Regex.Split(input, @"\W").Distinct().ToList();
            input = input.Replace("\r\n", ",");
            IList<string> numbers = input.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            return numbers.Distinct().ToList();
        }

        public static List<string> FormatNumbers(List<string> numbers)
        {
            List<string> formatedNumbers = new List<string>();
            using (var db = new ApplicationDbContext())
            {
                var codes = db.DialCodes.Include(x => x.PriceSetting).ToList();

                foreach (var code in codes)
                {
                    var getNumbers = numbers.Where(x => x.StartsWith(code.NumberPrefix));
                    foreach (var item in getNumbers.ToList())
                    {
                        string newItem;
                        if (item.StartsWith("0"))
                        {
                            newItem = item.Substring(1);
                            newItem = code.PriceSetting.InternationalDialCode + newItem;
                        }
                        else
                        {
                            newItem = code.PriceSetting.InternationalDialCode + item;
                        }

                        formatedNumbers.Add(newItem);
                        numbers.Remove(item);
                    }
                }

                if (numbers.Count() > 0)
                {
                    foreach (var num in numbers)
                    {
                        formatedNumbers.Add(num);
                    }
                }
            }

            return formatedNumbers;
        }

        public static decimal UnitsPerPage(List<string> numbers)
        {
            decimal units = 0;
            using (var db = new ApplicationDbContext())
            {
                var settings = db.AdminSettings.AsNoTracking().FirstOrDefault();
                var codes = db.DialCodes.Include(x => x.PriceSetting).ToList();

                foreach (var item in codes)
                {
                    var selectNumbers = numbers.Where(x => x.StartsWith(item.PriceSetting.InternationalDialCode + RemoveZero(item.NumberPrefix)));
                    units = units + (selectNumbers.Count() * item.PriceSetting.UnitsPerSms);

                    foreach (var number in selectNumbers.ToList())
                    {
                        numbers.Remove(number);
                    }
                }

                if (numbers.Count() > 0)
                {
                    units = units + numbers.Count() * settings.FlatUnitsPerSms;
                }

                return units;
            }
        }

        public static string RemoveZero(string number)
        {
            if (number.StartsWith("0"))
            {
                number = number.Substring(1);
            }

            return number;
        }

        public static int CountPage(string input)
        {
            int pageCount = 0;
            int remainder, inputCount = Math.DivRem(input.Length, 160, out remainder);

            if (remainder > 0)
            {
                pageCount = inputCount + 1;
            }
            else
            {
                pageCount = inputCount;
            }

            return pageCount;
        }
    }
}