using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllItEbooksCrawler
{
    public class Book: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }
        public int PostId { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string Url { get; set; }
        public string DownloadUrl { get; set; }
        public string LocalPath { get; set; }
        public string Authors { get; set; }
        public string Summary { get; set; }
        private string _category { get; set; }
        public string Category { get { return _category; } set { _category = value; OnPropertyChanged("Category"); } }
        public string OldCategory { get; set; }
        public string ISBN { get; set; }
        public int Pages { get; set; }

        private int _approved;
        public int Approved { get { return _approved; } set { _approved = value; OnPropertyChanged("Approved"); } }

        public bool _suggested;
        public bool Suggested { get { return _suggested; } set { _suggested = value; OnPropertyChanged("Suggested");  } }

        private bool _isChecked;
        public bool IsChecked { get { return _isChecked; } set { _isChecked = value; OnPropertyChanged("IsChecked"); } }
        public override string ToString()
        {
            return $"[{Year}] {Authors}. {Title}";
        }
    }
}
