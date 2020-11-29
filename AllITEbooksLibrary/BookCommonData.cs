using AllITEbooksLib.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BookUtils
{
    public class BookCommonData
    {
        public BookCommonData() { }

        public static string BOOKS_ROOT = Settings.Default.BooksRoot;
        public static string GOOGLE_DRIVE_DB_PATH = Settings.Default.GoogleDriveBooksPath;
        public static string DB_PATH = Settings.Default.DbPath;
        public static string SETTINGS_PATH = Settings.Default.SettingsPath;
        public static string SOURCE = "ORG"; //"IN"  


        public static ObservableRangeCollection<string> Categories { get; set; } = new ObservableRangeCollection<string>();

        public static void AddCategory(string category)
        {
            Categories.Add(category);
            var sorted = Categories.OrderBy(x => x).ToList();
            Categories.Clear();
            Categories.AddRange(sorted);
        }

        public static List<string> Hidden { get; set; } = new List<string>();
        public static Dictionary<string, string> Corrections { get; set; } = new Dictionary<string, string>();
        public static Dictionary<string, string> Suggestions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Open a file/url
        /// </summary>
        /// <param name="uri"></param>
        public static void OpenProcess(string uri)
        {
            if (uri == null)
                return;
            try
            {
                Process.Start(uri);
            }
            catch (Exception e)
            {
                MessageBox.Show("Exception while opening " + e.Message);
            }
        }

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
            if (File.Exists(Path.Combine(BookCommonData.SETTINGS_PATH, "suggestions.txt")))
            {
                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(Path.Combine(BookCommonData.SETTINGS_PATH, "suggestions.txt")))
                {
                    var split = line.Split('=');
                    dict.Add(split[0], split[1]);
                }
                Suggestions = dict;
            }
        }

        public static string UnrarFile(string rarFile)
        {
            string destFolder = Path.GetDirectoryName(rarFile);
            var p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "\"C:\\Program Files\\WinRAR\\winrar.exe\"";
            p.StartInfo.Arguments = string.Format(@"x -s ""{0}"" *.* ""{1}\""", rarFile, destFolder);
            p.Start();
            p.WaitForExit();
            var files = Directory.GetFiles(destFolder, Path.GetFileNameWithoutExtension(rarFile) + ".*").ToList();
            if (files.Count > 1)
            {
                files.Sort();
                if (files[0].Contains(".epub") && files.Count > 2)
                {
                    var index = files.FindIndex(f => f.Contains(".pdf"));
                    if (index > 0)
                        return files[index];
                    else
                        return files[0];
                }
                return files[0];
            }
            else
                throw new Exception("File not found");
        }
    }
}
