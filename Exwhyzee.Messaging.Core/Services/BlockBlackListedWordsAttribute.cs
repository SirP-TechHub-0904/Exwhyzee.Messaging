using Exwhyzee.Messaging.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace Exwhyzee.Messaging.Core.Services
{
    public class BlockBlackListedWordsAttribute : ValidationAttribute
    {
        // Thread-safe in-memory cache to avoid querying live remote database on every validation
        private static string _cachedBlackListPattern = null;
        private static DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly object _lock = new object();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private string GetBlackListPattern()
        {
            if (_cachedBlackListPattern != null && (DateTime.UtcNow - _lastCacheTime) < CacheDuration)
            {
                return _cachedBlackListPattern;
            }

            lock (_lock)
            {
                if (_cachedBlackListPattern != null && (DateTime.UtcNow - _lastCacheTime) < CacheDuration)
                {
                    return _cachedBlackListPattern;
                }

                try
                {
                    using (ApplicationDbContext db = new ApplicationDbContext())
                    {
                        var setting = db.AdminSettings.FirstOrDefault();
                        if (setting != null && !string.IsNullOrWhiteSpace(setting.BlackListedWords))
                        {
                            var words = setting.BlackListedWords
                                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(w => Regex.Escape(w.Trim()))
                                .Where(w => !string.IsNullOrWhiteSpace(w))
                                .ToList();

                            if (words.Any())
                            {
                                _cachedBlackListPattern = string.Join("|", words);
                            }
                            else
                            {
                                _cachedBlackListPattern = string.Empty;
                            }
                        }
                        else
                        {
                            _cachedBlackListPattern = string.Empty;
                        }

                        _lastCacheTime = DateTime.UtcNow;
                    }
                }
                catch (Exception)
                {
                    // If live server has a transport/network drop, fallback to existing cache or bypass to avoid crashing
                    return _cachedBlackListPattern ?? string.Empty;
                }

                return _cachedBlackListPattern;
            }
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            string input = value as string;

            if (!string.IsNullOrWhiteSpace(input))
            {
                try
                {
                    string pattern = GetBlackListPattern();
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        Regex wordFilter = new Regex(pattern, RegexOptions.IgnoreCase);
                        if (wordFilter.IsMatch(input))
                        {
                            return new ValidationResult("Content contains blacklisted words. Please review your content and try again.");
                        }
                    }
                }
                catch (Exception)
                {
                    // Fallback gracefully on regex or lookup error
                    return ValidationResult.Success;
                }
            }

            return ValidationResult.Success;
        }
    }
}

