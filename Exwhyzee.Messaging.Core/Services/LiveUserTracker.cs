using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Exwhyzee.Messaging.Core.Services
{
    /// <summary>
    /// Thread-safe in-memory tracker for live dashboard users via JavaScript heartbeat.
    /// </summary>
    public static class LiveUserTracker
    {
        private static readonly ConcurrentDictionary<string, DateTime> _onlineUsers =
            new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private const int DefaultTimeoutSeconds = 45;

        public static int RecordHeartbeat(string userKey, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!string.IsNullOrWhiteSpace(userKey))
            {
                _onlineUsers[userKey.Trim()] = DateTime.UtcNow;
            }

            PruneStaleUsers(timeoutSeconds);
            return Math.Max(1, _onlineUsers.Count);
        }

        public static void RemoveUser(string userKey)
        {
            if (!string.IsNullOrWhiteSpace(userKey))
            {
                _onlineUsers.TryRemove(userKey.Trim(), out _);
            }
        }

        public static int GetOnlineUsersCount(int timeoutSeconds = DefaultTimeoutSeconds)
        {
            PruneStaleUsers(timeoutSeconds);
            return Math.Max(1, _onlineUsers.Count);
        }

        public static List<string> GetOnlineUsersList(int timeoutSeconds = DefaultTimeoutSeconds)
        {
            PruneStaleUsers(timeoutSeconds);
            return _onlineUsers.Keys.OrderBy(k => k).ToList();
        }

        private static void PruneStaleUsers(int timeoutSeconds)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-timeoutSeconds);
            foreach (var kvp in _onlineUsers)
            {
                if (kvp.Value < cutoff)
                {
                    _onlineUsers.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}

