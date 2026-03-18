using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SemanticSearch.Editor.Core.Watcher
{
    public static class AssetFilter
    {
        static readonly string[] HardcodedBlacklist = { "Packages/", "Library/" };

        public static bool IsIncluded(string assetPath, List<string> includeFilters, List<string> excludeFilters)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            foreach (var prefix in HardcodedBlacklist)
            {
                if (assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (excludeFilters != null)
            {
                foreach (var pattern in excludeFilters)
                {
                    if (!string.IsNullOrWhiteSpace(pattern) && GlobMatch(assetPath, pattern))
                        return false;
                }
            }

            if (includeFilters == null || includeFilters.Count == 0)
                return true;

            foreach (var pattern in includeFilters)
            {
                if (!string.IsNullOrWhiteSpace(pattern) && GlobMatch(assetPath, pattern))
                    return true;
            }

            return false;
        }

        private static readonly Dictionary<string, Regex> RegexCache = new Dictionary<string, Regex>();

        /// <summary>
        /// Lightweight glob matching supporting * (single segment) and ** (recursive).
        /// Path separators are normalized to '/'.
        /// </summary>
        public static bool GlobMatch(string path, string pattern)
        {
            if (!RegexCache.TryGetValue(pattern, out var regex))
            {
                bool implicitRecursive = pattern.EndsWith("/") || pattern.EndsWith("\\");
                string normalizedPattern = NormalizePath(pattern);

                if (implicitRecursive)
                    normalizedPattern += "/**";

                var regexPattern = GlobToRegex(normalizedPattern);
                regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                RegexCache[pattern] = regex;
            }

            path = NormalizePath(path);
            return regex.IsMatch(path);
        }

        static string NormalizePath(string p)
        {
            return p.Replace('\\', '/').TrimEnd('/');
        }

        static string GlobToRegex(string glob)
        {
            var parts = new System.Text.StringBuilder("^");
            int i = 0;
            while (i < glob.Length)
            {
                char c = glob[i];
                if (c == '*')
                {
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        if (i + 2 < glob.Length && glob[i + 2] == '/')
                        {
                            parts.Append("(.+/)?");
                            i += 3;
                        }
                        else
                        {
                            parts.Append(".*");
                            i += 2;
                        }
                    }
                    else
                    {
                        parts.Append("[^/]*");
                        i++;
                    }
                }
                else if (c == '?')
                {
                    parts.Append("[^/]");
                    i++;
                }
                else
                {
                    parts.Append(Regex.Escape(c.ToString()));
                    i++;
                }
            }
            parts.Append("$");
            return parts.ToString();
        }
    }
}
