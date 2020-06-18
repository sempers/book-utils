using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllItEbooksCrawler
{
    public static class Extensions
    {
        public static string PadKey(this string key)
        {
            var num = key.Count(c => c == '/');
            for (int i = 0; i < num; i++)
            {
                key = "   " + key;
            }
            return key.PadRight(60, '.');
        }

        public static bool StartsWithSlash(this string s, string pattern)
        {
            return s == pattern || s.StartsWith(pattern + "/");
        }

        public static bool ContainsEvery(this string s, string[] array)
        {
            bool result = true;
            foreach (string str in array)
            {
                if (string.IsNullOrEmpty(str))
                    continue;
                result = result && s.Contains(str.Trim());
                if (!result)
                    break;
            }
            return result;
        }

        public static bool ContainsAny(this string s, string[] array)
        {
            bool result = false;
            foreach (string str in array)
            {
                if (string.IsNullOrEmpty(str))
                    continue;
                result = result || s.Contains(str.Trim());
                if (result)
                    break;
            }
            return result;
        }
    }
}
