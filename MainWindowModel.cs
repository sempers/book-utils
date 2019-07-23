using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllItEbooksCrawler
{
    public class MainWindowModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _message;
        public string Message { get { return _message; } set { _message = value; OnPropertyChanged("Message"); } }

        private string _searchTitle;
        public string SearchTitle { get { return _searchTitle; } set { _searchTitle = value; OnPropertyChanged("SearchTitle"); } }

        private bool _filterMode;
        public bool FilterMode { get { return _filterMode; } set { _filterMode = value; OnPropertyChanged("FilterMode"); } }

        public ObservableCollection<Book> ShownBooks { get; set; }

        public ObservableCollection<string> Categories { get; set; }

        public HashSet<string> Sortings = new HashSet<string>();

        public List<Book> Books;

        public MainWindowModel()
        {
            ShownBooks = new ObservableCollection<Book>();
            Books = new List<Book>();
            Categories = new ObservableCollection<string>();
            FilterMode = true;
        }

        public void LoadList(List<Book> list)
        {
            ShownBooks.Clear();
            foreach (var book in list)
            {
                ShownBooks.Add(book);
            }
        }

        private int GetSorting(string column)
        {
            if (Sortings.Contains(column))
            {
                Sortings.Remove(column);
                return -1;
            }
            else
            {
                Sortings.Add(column);
                return 1;
            }
        }

        public void FilterListByCategory(string category)
        {
            if (category == "(no category)" || string.IsNullOrEmpty(category))
            {
                if (Books != null)
                {
                    LoadList(Books);
                }
            }
            else
            {
                var filteredList = Books.FindAll(book => book.Category == category).ToList();
                LoadList(filteredList);
            }
        }

        public void FilterListByTitle(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                if (Books != null)
                {
                    LoadList(Books);
                }
            }
            else
            {
                var filteredList = Books.FindAll(book => book.Title.ToUpper().Contains(search.ToUpper())).ToList();
                LoadList(filteredList);
            }
        }

        public void SortList(string column)
        {
            List<Book> sortedList = null;
            switch (column)
            {
                case "PostId":
                if (GetSorting("PostId") < 0)
                    sortedList = ShownBooks.OrderByDescending(b => b.PostId).ToList();
                else
                    sortedList = ShownBooks.OrderBy(b => b.PostId).ToList();
                break;
                case "Title":
                if (GetSorting("Title") < 0)
                    sortedList = ShownBooks.OrderByDescending(b => b.Title).ToList();
                else
                    sortedList = ShownBooks.OrderBy(b => b.Title).ToList();
                break;
                case "Year":
                if (GetSorting("Year") < 0)
                    sortedList = ShownBooks.OrderByDescending(b => b.Year).ToList();
                else
                    sortedList = ShownBooks.OrderBy(b => b.Year).ToList();
                break;
                case "Category":
                if (GetSorting("Category") < 0)
                    sortedList = ShownBooks.OrderByDescending(b => b.Category).ToList();
                else
                    sortedList = ShownBooks.OrderBy(b => b.Category).ToList();
                break;
            }
            if (sortedList != null)
                LoadList(sortedList);
        }
    }
}
