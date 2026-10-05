using Exwhyzee.Messaging.Core.Models;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.RegularExpressions;


namespace Exwhyzee.Messaging.Core.Services
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
            if (numbers == null || !numbers.Any()) return 0;

            var remainingNumbers = new List<string>(numbers);
            decimal units = 0;
            using (var db = new ApplicationDbContext())
            {
                var settings = db.AdminSettings.AsNoTracking().FirstOrDefault();
                decimal flatRate = settings?.FlatUnitsPerSms ?? 4.0m;
                var codes = db.DialCodes.Include(x => x.PriceSetting).AsNoTracking().ToList();

                foreach (var item in codes)
                {
                    if (item.PriceSetting == null) continue;
                    string prefix = (item.PriceSetting.InternationalDialCode ?? "234") + RemoveZero(item.NumberPrefix);
                    var matched = remainingNumbers.Where(x => x.StartsWith(prefix)).ToList();
                    if (matched.Any())
                    {
                        units += (matched.Count * item.PriceSetting.UnitsPerSms);
                        foreach (var m in matched)
                        {
                            remainingNumbers.Remove(m);
                        }
                    }
                }

                if (remainingNumbers.Count > 0)
                {
                    units += remainingNumbers.Count * flatRate;
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
            if (string.IsNullOrEmpty(input)) return 0;
            
            // Normalize HTTP \r\n to standard SMS \n (1 character)
            input = input.Replace("\r\n", "\n");

            bool isUnicode = System.Text.RegularExpressions.Regex.IsMatch(input, @"[^\u0000-\u007F]");
            int pageLimit = isUnicode ? 70 : 160;
            int pageLimitConcat = isUnicode ? 67 : 153;

            if (input.Length <= pageLimit)
            {
                return 1;
            }

            return (int)Math.Ceiling((double)input.Length / pageLimitConcat);
        }
    }
}


