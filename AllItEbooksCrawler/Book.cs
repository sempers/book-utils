using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookUtils
{
    public class Book : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static string ROOT = null;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }
        public int PostId { get; set; }

        private string _title;
        public string Title { get => _title; set { _title = value; OnPropertyChanged("Title"); } }

        private int _year;
        public int Year { get => _year; set { _year = value; OnPropertyChanged("Year"); } }

        public string Url { get; set; }
        public string DownloadUrl { get; set; }
        public string Authors { get; set; }
        public string Summary { get; set; }

        private string _category { get; set; }
        public string Category
        {
            get => _category; set
            {
                _category = value;
                OnPropertyChanged("Category");
            }
        }

        public string Extension { get; set; }

        private int _approved;
        public int Approved { get => _approved; set { _approved = value; OnPropertyChanged("Approved"); } }


        public bool _suggested;
        [NotMapped]
        public bool Suggested { get => _suggested; set { _suggested = value; OnPropertyChanged("Suggested"); } }

        public int _rating;
        public int Rating { get => _rating; set { _rating = value; OnPropertyChanged("Rating"); } }

        public static ObservableCollection<string> Categories { get; set; } = new ObservableCollection<string>();

        public void SetCategory(string category, bool approve = false)
        {
            Category = category;
            var oldPath = IsDownloaded ? LocalPath : null;
            if (Approved == 0 && approve)
            {
                Approved = 1;
            }
            if (Suggested)
            {
                Suggested = false;
            }

            if (oldPath != null && oldPath != LocalPath)
            {
                AutoMove(oldPath);
            }
        }

        public static void AddCategory(string category)
        {
            Categories.Add(category);
            var sorted = Book.Categories.OrderBy(x => x).ToList();
            Book.Categories.Clear();
            foreach (var s in sorted)
            {
                Book.Categories.Add(s);
            }
        }

        public string OldCategory;
        public string ISBN { get; set; }
        public int Pages { get; set; }
        public int Sync { get; set; }

        private bool _isChecked;
        [NotMapped]
        public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged("IsChecked"); } }

        private bool _downloadedGUI;
        [NotMapped]
        public bool DownloadedGUI { get => _downloadedGUI; set { _downloadedGUI = value; OnPropertyChanged("DownloadedGUI"); } }

        public override string ToString()
        {
            return $"[{Year}] {ClearTitle} - {ClearAuthors}";
        }

        private static string ClearString(string s)
        {
            return s.Trim().Replace(":", " ").Replace("\"", "'").Replace("|", " ").Replace("?", "");
        }

        public string ClearAuthors
        {
            get
            {
                var src = ClearString(Authors);
                var split = src.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 2)
                    return $"{split[0]}, {split[1]} et al";
                else
                    return src;
            }
        }

        public string ClearTitle => ClearString(Title);

        public string ClearFileName => $"[{Year}] {ClearTitle} - {ClearAuthors}.{Extension}";

        public string FirstCategory => Category == null ? "" : Category.Contains(";") ? Category.Split(';')[0] : Category;

        public string LocalPath => Path.Combine(ROOT, FirstCategory.Replace("/", "\\"), ClearFileName);

        public bool IsDownloaded { get { var val = string.IsNullOrEmpty(LocalPath) ? false : File.Exists(LocalPath); DownloadedGUI = val;  return val; } }

        private void AutoMove(string oldPath)
        {
            if (oldPath == null)
                return;
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(LocalPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));
                File.Move(oldPath, LocalPath);
            }
            catch
            {

            }
        }
    }
}

