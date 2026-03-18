using System;

namespace SemanticSearch.Editor.Core.Utils
{
    internal static class FormatUtils
    {
        public static string FormatBytes(long bytes)
        {
            const double kb = 1024d;
            const double mb = kb * 1024d;
            if (Math.Abs(bytes) >= mb)
                return $"{bytes / mb:F2} MB";
            if (Math.Abs(bytes) >= kb)
                return $"{bytes / kb:F2} KB";
            return $"{bytes} B";
        }
    }
}
