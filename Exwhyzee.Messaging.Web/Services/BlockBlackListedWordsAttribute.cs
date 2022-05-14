using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Exwhyzee.Messaging.Web.Services
{
    public class BlockBlackListedWordsAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            string input = value as string;

            if (input != null)
            {
                using (ApplicationDbContext db = new ApplicationDbContext())
                {
                    var blackLists = db.AdminSettings.FirstOrDefault().BlackListedWords.Replace(",", "|");
                    blackLists = "(" + blackLists + ")";
                    Regex wordFilter = new Regex(blackLists);
                    if (wordFilter.IsMatch(input))
                    {
                        return new ValidationResult("Content contains a Blacklisted words. Please review your content and try again.");
                    }
                }
            }

            return ValidationResult.Success;
        }
    }
}