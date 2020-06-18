using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookUtils
{
    public class CommonData
    {
        public const string BOOKS_ROOT = @"D:\books\it";
        public const string GOOGLE_DRIVE_DB_PATH = @"D:\books\google_drive\itdb\books.db";
        public const string DB_PATH = @"books.db";
        public const string SETTINGS_PATH = @"..\..\settings\";

        public static ObservableRangeCollection<string> Categories { get; set; } = new ObservableRangeCollection<string>();

        public static void AddCategory(string category)
        {
            Categories.Add(category);
            var sorted = Categories.OrderBy(x => x).ToList();
            Categories.Clear();
            Categories.AddRange(sorted);
        }

        public CommonData() { }

        public static List<string> Hidden { get; set; } = new List<string>();
        public static Dictionary<string, string> Corrections { get; set; } = new Dictionary<string, string>();
        public static Dictionary<string, string> Suggestions { get; set; } = new Dictionary<string, string>();

        public static void LoadCorrections()
        {
            if (File.Exists(Path.Combine(SETTINGS_PATH, "corrections.txt")))
            {
                Corrections = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(Path.Combine(SETTINGS_PATH, "corrections.txt")))
                {
                    if (!line.Contains("="))
                        continue;
                    var split = line.Split('=');
                    Corrections.Add(split[0], split[1]);
                }
            }
        }

        public static void LoadHidden()
        {
            if (File.Exists(Path.Combine(SETTINGS_PATH, "hidden.txt")))
            {
                Hidden = File.ReadAllLines(Path.Combine(SETTINGS_PATH, "hidden.txt")).ToList();
            }
        }

        public static bool HiddenIncludes(Book b)
        {
            foreach (var line in Hidden)
            {
                if (line.Contains("*") && b.FirstCategory.Contains(line.Replace("*", "")) || b.FirstCategory == line)
                    return true;
            }
            return false;
        }

        public static void LoadSuggestions()
        {
            if (File.Exists(Path.Combine(CommonData.SETTINGS_PATH, "suggestions.txt")))
            {
                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(Path.Combine(CommonData.SETTINGS_PATH, "suggestions.txt")))
                {
                    var split = line.Split('=');
                    dict.Add(split[0], split[1]);
                }
                Suggestions = dict;
            }
        }
    }
}
