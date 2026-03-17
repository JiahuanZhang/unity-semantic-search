using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SemanticSearch.Editor.Core.Watcher
{
    public static class MD5Helper
    {
        public static string ComputeFileMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = md5.ComputeHash(stream);
                var sb = new StringBuilder(32);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
