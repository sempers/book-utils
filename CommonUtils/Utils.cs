using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CommonUtils
{
    public static class Utils
    {
        private static object _lock = new object();

        public static Dictionary<string, string> LoadMapping(string filename)
        {
            var lines = File.ReadAllLines(filename);
            var result = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                result.Add(parts[0].Trim(), parts[1].Trim());
            }
            return result;
        }

        public static void MakeDir(string dir)
        {
            dir = Path.GetFullPath(dir);
            if (Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static List<string> ReadLines(string path)
        {
            var result = new List<string>();
            if (File.Exists(path))
            {
                result = File.ReadAllLines(path).ToList();
            }
            return result;
        }

        public static void DeleteFile(string path)
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        if (fi.IsReadOnly)
                            fi.IsReadOnly = false;
                        File.Delete(path);
                    }
                }
                catch { }
            }
        }

        public static void FileCopy(string srcPath, string destPath)
        {
            if (File.Exists(srcPath))
            {
                CreateDirectory(Path.GetDirectoryName(destPath));
                if (File.Exists(destPath))
                    DeleteFile(destPath);
                File.Copy(srcPath, destPath);
            }
        }

        public static void FileMove(string srcPath, string destPath)
        {
            if (File.Exists(srcPath))
            {
                CreateDirectory(Path.GetDirectoryName(destPath));
                if (File.Exists(destPath))
                    DeleteFile(destPath);
                File.Move(srcPath, destPath);
            }
        }

        public static DateTime FromJSTicks(int ticks)
        {
            var jan1st = new DateTime(1970, 1, 1);
            return jan1st.AddMilliseconds((long)ticks * 1000).ToLocalTime();
        }

        /// <summary>
        /// Возвращает форматированный размер файла
        /// </summary>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public static string FormatFileSize(decimal fileSize)
        {
            var fs = Convert.ToDouble(fileSize);
            if (fileSize < 1024)
                return string.Format("{0} Б", fs.ToString("0"));
            return fileSize < 1024 * 1024 ? string.Format("{0} Кб", (fs / 1024).ToString("0.#")) : string.Format("{0} Мб", (fs / (1024 * 1024)).ToString("0.#"));
        }

        /// <summary>
        /// Создать директорию, если не существует
        /// </summary>
        /// <param name="destDir"></param>
        public static void CreateDirectory(string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
        }

        /// <summary>
        /// Удаление папки
        /// </summary>
        /// <param name="path">папка</param>
        /// <param name="removeSelf">Удалить саму себя</param>
        /// <returns>Успех/неуспех</returns>
        public static string CleanFolder(string path, bool removeSelf = true)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);

                foreach (var fileInfo in dirInfo.GetFiles())
                {
                    if (fileInfo.IsReadOnly)
                        fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                }

                foreach (var dInfo in dirInfo.GetDirectories())
                {
                    var done = CleanFolder(dInfo.FullName);
                    if (done != "OK")
                        return done;
                }

                if (removeSelf)
                    dirInfo.Delete(true);

                return "OK";
            }
            catch (Exception e)
            {
                return e.Message + "/" + e.StackTrace;
            }
        }

        /// <summary>
        /// Копирование директорий
        /// </summary>
        /// <param name="sourceDir">Исходная папка</param>
        /// <param name="destDir">Целевая папка</param>
        public static bool CopyFolder(string sourceDir, string destDir, List<Tuple<string, string>> copyList = null)
        {
            var sourceDirInfo = new DirectoryInfo(sourceDir);
            CreateDirectory(destDir);

            var subDirInfos = sourceDirInfo.GetDirectories();
            var files = sourceDirInfo.GetFiles();

            foreach (var file in files)
            {
                try
                {
                    file.CopyTo(Path.Combine(destDir, file.Name), true);
                }
                catch (IOException e)
                {
                    //fix 30.11.2016 чтобы не останавливался процесс на проектах решений, которые может редактировать секретарь КК
                    if (!file.FullName.Contains("decision"))
                        throw;
                }
            }

            foreach (var subdir in subDirInfos)
            {
                CopyFolder(subdir.FullName, Path.Combine(destDir, subdir.Name));
            }

            //FIX 25.12.2015
            //переносим материалы из исходного места
            if (copyList != null)
            {
                foreach (Tuple<string, string> tuple in copyList)
                {
                    File.Copy(tuple.Item1, tuple.Item2, true);
                }
            }

            return true;
        }

        /// <summary>
        /// Захешировать содержимое файла
        /// </summary>
        /// <param name="path">Путь к файлу</param>
        /// <returns>Шестнадцатеричный хеш</returns>
        public static string HashFileContent(string path)
        {
            using (var md5Hasher = MD5.Create())
            {
                var hashData = md5Hasher.ComputeHash(File.ReadAllBytes(path));
                var sb = new StringBuilder();
                foreach (byte t in hashData)
                {
                    sb.Append(t.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Безопасное форматирование имени файла
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string FormatFileName(object path)
        {
            if (path is DBNull)
                return "";
            if (path is string)
                return Path.GetFileName(path.ToString());
            return "";
        }

        /// <summary>
        /// Латинизация имени файла
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string LatinizeFileName(string path)
        {
            string[,] replacementTable = {
                {"а","a"}, {"А","A"},
                {"б","b"}, {"Б","B"},
                {"в","v"}, {"В","V"},
                {"г","g"}, {"Г","G"},
                {"д","d"}, {"Д","D"},
                {"е","e"}, {"Е","E"},
                {"ё","yo"}, {"Ё","Yo"},
                {"ж","zh"}, {"Ж","Zh"},
                {"з","z"}, {"З","Z"},
                {"и","i"}, {"И","I"},
                {"й","y"}, {"Й","Y"},
                {"к","k"}, {"К","K"},
                {"л","l"}, {"Л","L"},
                {"м","m"}, {"М","M"},
                {"н","n"}, {"Н","N"},
                {"о","o"}, {"О","O"},
                {"п","p"}, {"П","P"},
                {"р","r"}, {"Р","R"},
                {"с","s"}, {"С","S"},
                {"т","t"}, {"Т","T"},
                {"у","u"}, {"У","U"},
                {"ф","f"}, {"Ф","F"},
                {"х","h"}, {"Х","H"},
                {"ц","ts"}, {"Ц","Ts"},
                {"ч","ch"}, {"Ч","Ch"},
                {"ш","sh"}, {"Ш","Sh"},
                {"щ","shch"}, {"Щ","Shch"},
                {"ъ",""}, {"Ъ",""},
                {"ы","y"}, {"Ы","Y"},
                {"ь",""}, {"Ь",""},
                {"э","e"}, {"Э","E"},
                {"ю","yu"}, {"Ю","Yu"},
                {"я","ya"}, {"Я","Ya"}
            };

            for (var i = 0; i < (replacementTable.Length / 2); i++)
            {
                var cyr = replacementTable[i, 0];
                var lat = replacementTable[i, 1];
                path = path.Replace(cyr, lat);
            }
            return path;
        }
    }
}
