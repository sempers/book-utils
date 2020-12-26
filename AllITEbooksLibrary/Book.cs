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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }
        public int PostId { get; set; }

        private int? _obsolete;
        [Column("NewEditionPostId")]
        public int? Obsolete { get => _obsolete; set { _obsolete = value; OnPropertyChanged("Obsolete"); } }

        private int _edition;
        public int Edition { get => _edition; set { _edition = value; OnPropertyChanged("Edition"); } }

        private string _title;
        public string Title { get => _title; set { _title = value; OnPropertyChanged("Title"); } }

        [NotMapped]
        public string TitleWOEdition { get; set; }

        private int _year;
        public int Year { get => _year; set { _year = value; OnPropertyChanged("Year"); } }


        private string _url;
        public string Url { get => _url; set { _url = value; OnPropertyChanged("Url"); } }

        private string _downloadUrl;
        public string DownloadUrl { get => _downloadUrl; set { _downloadUrl = value; OnPropertyChanged("DownloadUrl"); } }
        public string Authors { get; set; }

        private string _summary;
        public string Summary { get => _summary; set { _summary = value; OnPropertyChanged("Summary"); } }

        private string _category { get; set; }
        public string Category
        {
            get => _category;
            set
            {
                var oldPath = IsDownloaded ? LocalPath : null;
                _category = value;
                if (oldPath != null && oldPath != LocalPath)
                {
                    AutoMove(oldPath);
                }
                OnPropertyChanged("Category");
            }
        }

        string _ext;
        public string Extension { get => _ext; set { _ext = value; OnPropertyChanged("Extension"); } }

        private int _approved;
        public int Approved { get => _approved; set { _approved = value; OnPropertyChanged("Approved"); } }

        public bool _suggested;
        [NotMapped]
        public bool Suggested { get => _suggested; set { _suggested = value; OnPropertyChanged("Suggested"); } }

        public int _rating;
        public int Rating { get => _rating; set { _rating = value; OnPropertyChanged("Rating"); } }

        public void SetCategory(string category, bool approve = false)
        {
            if (category == null)
                return;

            Category = category;
            if (Approved == 0 && approve)
            {
                Approved = 1;
            }
            if (Suggested)
            {
                Suggested = false;
            }
        }

        public string OldCategory;

        private string _isbn;
        public string ISBN { get => _isbn; set { _isbn = value; InferPublisher(); OnPropertyChanged("ISBN"); } }

        private int _pages;
        public int Pages { get => _pages; set { _pages = value; OnPropertyChanged("Pages"); } }

        private int _read;
        public int Read
        {
            get => _read;
            set
            {
                _read = value; OnPropertyChanged("Read");
            }
        }

        private bool _readGUI;
        [NotMapped]
        public bool ReadGUI
        {
            get => _readGUI;
            set
            {
                _readGUI = value; Read = _readGUI ? 1 : 0; OnPropertyChanged("ReadGUI");
            }
        }

        private string _pub;
        [Column("Pub")]
        public string Publisher
        {
            get => _pub;
            set { _pub = value; OnPropertyChanged("Publisher"); }
        }

        private string _source;
        public string Source
        {
            get => _source;
            set { _source = value; OnPropertyChanged("Source"); }
        }

        private byte[] _cover;
        public byte[] Cover
        {
            get => _cover;
            set { _cover = value; OnPropertyChanged("Cover"); }
        }

        private string FromISBN()
        {
            var isbn = ISBN;
            if (isbn == null) return "";
            isbn = isbn.Trim().Replace("978-", "");
            if (isbn.StartsWith("978"))
                isbn = isbn.Replace("978", "");
            if (isbn.StartsWith("149") || isbn.StartsWith("1449")) return "OR";
            else if (isbn.StartsWith("14842")) return "AP";
            else if (isbn.StartsWith("17185") || isbn.StartsWith("15932")) return "NS";
            else if (isbn.StartsWith("1119")) return "WL";
            else if (isbn.StartsWith("16172")) return "MA";
            else if (isbn.StartsWith("178")) return "PK";
            else if (isbn.StartsWith("11186")) return "SY";
            else if (isbn.StartsWith("3319")) return "SP";
            else return "";
        }

        private void InferPublisher()
        {
            if (string.IsNullOrEmpty(Publisher))
                Publisher = FromISBN();
        }

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
            if (s == null)
                return "";
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
        public string LocalPath
        {
            get
            {
                if (DownloadUrl == null)
                    return "";
                if (DownloadUrl.StartsWith("http"))
                    return Path.Combine(BookCommonData.BOOKS_ROOT, FirstCategory.Replace("/", "\\"), ClearFileName);
                else
                    return DownloadUrl;
            }
        }
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

