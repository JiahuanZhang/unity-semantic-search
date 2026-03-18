using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace SemanticSearch.Editor.Core.Watcher
{
    public static class MD5Helper
    {
        class FileHashCacheEntry
        {
            public long Length;
            public long LastWriteUtcTicks;
            public string Md5;
        }

        static readonly object CacheLock = new object();
        static readonly Dictionary<string, FileHashCacheEntry> Cache =
            new Dictionary<string, FileHashCacheEntry>(System.StringComparer.OrdinalIgnoreCase);
        const int MaxCacheEntries = 20000;

        public static string ComputeFileMD5(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            long length = fileInfo.Length;
            long lastWriteUtcTicks = fileInfo.LastWriteTimeUtc.Ticks;

            lock (CacheLock)
            {
                if (Cache.TryGetValue(filePath, out var entry) &&
                    entry.Length == length &&
                    entry.LastWriteUtcTicks == lastWriteUtcTicks)
                {
                    return entry.Md5;
                }
            }

            string md5Text;
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = md5.ComputeHash(stream);
                var sb = new StringBuilder(32);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                md5Text = sb.ToString();
            }

            lock (CacheLock)
            {
                Cache[filePath] = new FileHashCacheEntry
                {
                    Length = length,
                    LastWriteUtcTicks = lastWriteUtcTicks,
                    Md5 = md5Text
                };

                if (Cache.Count > MaxCacheEntries)
                {
                    int toRemove = Cache.Count / 2;
                    var keysToEvict = new List<string>(toRemove);
                    foreach (var key in Cache.Keys)
                    {
                        keysToEvict.Add(key);
                        if (keysToEvict.Count >= toRemove)
                            break;
                    }
                    foreach (var key in keysToEvict)
                        Cache.Remove(key);
                }
            }

            return md5Text;
        }
    }
}
