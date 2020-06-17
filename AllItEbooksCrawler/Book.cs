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

        private int? _newEditionPostId;
        public int? NewEditionPostId { get => _newEditionPostId; set { _newEditionPostId = value; OnPropertyChanged("NewEditionPostId"); } }

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
            get => _category;
            set
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

        public static ObservableRangeCollection<string> Categories { get; set; } = new ObservableRangeCollection<string>();

        public void SetCategory(string category, bool approve = false)
        {
            if (category == null)
                return;
            var oldPath = IsDownloaded ? LocalPath : null;
            Category = category;
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
            var sorted = Categories.OrderBy(x => x).ToList();
            Categories.Clear();
            Categories.AddRange(sorted);
        }

        public string OldCategory;
        public string ISBN { get; set; }

        private int _pages;
        public int Pages { get => _pages; set { _pages = value; OnPropertyChanged("Pages"); } }

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
            return s.Trim().Replace(":", "-").Replace("\"", "'").Replace("|", " ").Replace("?", "").Replace("/", "-");
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

        public bool IsDownloaded
        {
            get
            {
                var val = PostId < 0 ? File.Exists(DownloadUrl) : (string.IsNullOrEmpty(LocalPath) ? false : File.Exists(LocalPath));
                DownloadedGUI = val;
                return val;
            }
        }

        private void AutoMove(string oldPath)
        {
            if (oldPath == null || PostId < 0)
                return;
            try
            {
                CommonUtils.Utils.CreateDirectory(Path.GetDirectoryName(LocalPath));
                File.Move(oldPath, LocalPath);
            }
            catch
            {

            }
        }

        public void ToggleCheck()
        {
            IsChecked = !IsChecked;
        }

        public object GetValue(string propName, bool unwrap)
        {
            switch (propName)
            {
                case "Category": return Category;
                case "Title": return Title;
                default: throw new Exception($"{propName} is not supported");
            }
        }
    }
}

