using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookUtils
{
    public class Book : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static string root = null;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
        public string Category { get => _category; set { _category = value; OnPropertyChanged("Category"); } }

        private int _approved;
        public int Approved { get => _approved; set { _approved = value; OnPropertyChanged("Approved"); } }

        public bool _suggested;
        public bool Suggested { get => _suggested; set { _suggested = value; OnPropertyChanged("Suggested"); } }

        public int _rating;
        public int Rating { get => _rating; set { _rating = value; OnPropertyChanged("Rating"); } }

        public static ObservableCollection<string> Categories { get; set; } = new ObservableCollection<string>();

        public string OldCategory { get; set; }
        public string ISBN { get; set; }
        public int Pages { get; set; }

        public int Sync { get; set; }        

        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged("IsChecked"); } }

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

        public string ClearTitle
        {
            get
            {
                return ClearString(Title);
            }
        }

        public string PdfFileName
        {
            get { return $"[{Year}] {ClearTitle} - {ClearAuthors}.pdf"; }
        }

        public string FirstCategory
        {
            get
            {
                if (Category == null)
                    return "";
                return Category.Split(';')[0];
            }
        }

        public string LocalPath
        {
            get
            {
                return Path.Combine(root, FirstCategory.Replace("/", "\\"), PdfFileName);
            }
        }

        public bool IsDownloaded
        {
            get
            {
                if (string.IsNullOrEmpty(LocalPath))
                    return false;
                else
                    return File.Exists(LocalPath);
            }
        }

        public void AutoMove(string oldPath)
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

