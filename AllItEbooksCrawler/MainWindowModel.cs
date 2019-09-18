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
    public class BookFilter
    {
        public string Title { get; set; }
        public string Category { get; set; }
    }

    public class MainWindowModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _message;
        public string Message { get { return _message; } set { _message = value; OnPropertyChanged("Message"); } }

        private bool _filterMode;
        public bool FilterMode { get { return _filterMode; } set { _filterMode = value; OnPropertyChanged("FilterMode"); } }

        public BookFilter Filter = new BookFilter { Title = "", Category = "" };

        public ObservableCollection<Book> ShownBooks { get; set; }

        public HashSet<string> Sortings = new HashSet<string>();

        public List<Book> Books;

        public MainWindowModel()
        {
            ShownBooks = new ObservableCollection<Book>();
            Books = new List<Book>();
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

        public void ApplyFilter()
        {
            var filteredList = Books;
            if (!string.IsNullOrEmpty(Filter.Category))
            {
                filteredList = filteredList.FindAll(book => book.Category == Filter.Category).ToList();
            }
            else
            if (!string.IsNullOrEmpty(Filter.Title))
            {
                filteredList = filteredList.FindAll(book => book.Title.ToUpper().Contains(Filter.Title.ToUpper())).ToList();
            }
            LoadList(filteredList);
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

        public Dictionary<string, string> TxtCorrections { get; set; }
        public List<string> TxtHidden { get; set; }


        public void LoadCorrections()
        {
            if (File.Exists("../../settings/corrections.txt"))
            {
                TxtCorrections = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines("../../settings/corrections.txt"))
                {
                    if (!line.Contains("="))
                        continue;
                    var split = line.Split('=');
                    TxtCorrections.Add(split[0], split[1]);
                }
            }
        }

        public void LoadHidden()
        {
            if (File.Exists("../../settings/hidden.txt"))
            {
                TxtHidden = File.ReadAllLines("../../settings/hidden.txt").ToList();
            }
        }

        public bool HiddenIncludes(Book b)
        {
            foreach (var line in TxtHidden)
            {
                if (line.Contains("*") && b.FirstCategory.Contains(line.Replace("*", "")) || b.FirstCategory == line)
                    return true;
            }
            return false;
        }

        public void ListCategories()
        {
            var dict = new Dictionary<string, int>();
            foreach (var book in ShownBooks)
            {
                var firstCat = book.FirstCategory;
                if (!string.IsNullOrEmpty(firstCat))
                    if (!dict.ContainsKey(firstCat))
                        dict.Add(firstCat, 1);
                    else
                        dict[firstCat]++;
            }
            foreach (var add in File.ReadAllLines("../../settings/categories.txt"))
            {
                if (!string.IsNullOrEmpty(add) && !dict.ContainsKey(add))
                    dict.Add(add, 0);
            }
            var list = new List<string>();
            foreach (var kv in dict)
            {
                list.Add($"{kv.Key}");
            }
            list.Add("(no category)");
            list.Sort();
            Book.Categories.Clear();
            foreach (var cat in list)
            {
                Book.Categories.Add(cat);
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

        public bool UnsuggestCategories()
        {
            var was = false;
            foreach (var book in ShownBooks)
            {
                if (book.Suggested)
                {
                    book.Suggested = false;
                    book.Category = book.OldCategory;
                    was = true;
                }
            }
            return was;
        }

        public void DeleteEmptyFolders(string folder)
        {
            foreach (var dir in Directory.GetDirectories(folder))
            {
                DeleteEmptyFolders(dir);
                var files = Directory.GetFiles(dir);
                var dirs = Directory.GetDirectories(dir);
                if (files.Length == 0 && dirs.Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
        }

        public bool SuggestCategories()
        {
            if (File.Exists("../../settings/suggestions.txt"))
            {
                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines("../../settings/suggestions.txt"))
                {
                    var split = line.Split('=');
                    dict.Add(split[0], split[1]);
                }
                foreach (var book in ShownBooks.Where(book => book.Approved == 0))
                {
                    var titleWords = book.Title.Replace(",", "").Split(' ').ToList();
                    foreach (var kvPair in dict)
                    {
                        var keys = kvPair.Key.Split('|');
                        foreach (var key in keys)
                        {
                            if (!book.Suggested && titleWords.Contains(key) && !book.Category.Contains(kvPair.Value))
                            {
                                book.OldCategory = book.Category;
                                book.Category = kvPair.Value;
                                book.Suggested = true;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }
    }
}
