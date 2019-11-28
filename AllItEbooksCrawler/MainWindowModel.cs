using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;

namespace BookUtils
{
    public static class Extensions
    {
        public static bool ContainsEvery(this string s, string[] array)
        {
            bool result = true;
            foreach (string str in array)
            {
                result = result && s.Contains(str);
                if (!result)
                    break;
            }
            return result;
        }
    }
        
    public class ObservableRangeCollection<T>: ObservableCollection<T>
    {
        /// <summary> 
        /// Adds the elements of the specified collection to the end of the ObservableCollection(Of T). 
        /// </summary> 
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            foreach (var i in collection) Items.Add(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary> 
        /// Removes the first occurence of each item in the specified collection from ObservableCollection(Of T). 
        /// </summary> 
        public void RemoveRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            foreach (var i in collection) Items.Remove(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary> 
        /// Clears the current collection and replaces it with the specified item. 
        /// </summary> 
        public void Replace(T item)
        {
            ReplaceRange(new T[] { item });
        }

        /// <summary> 
        /// Clears the current collection and replaces it with the specified collection. 
        /// </summary> 
        public void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            Items.Clear();
            foreach (var i in collection) Items.Add(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary> 
        /// Initializes a new instance of the System.Collections.ObjectModel.ObservableCollection(Of T) class. 
        /// </summary> 
        public ObservableRangeCollection()
            : base() { }

        /// <summary> 
        /// Initializes a new instance of the System.Collections.ObjectModel.ObservableCollection(Of T) class that contains elements copied from the specified collection. 
        /// </summary> 
        /// <param name="collection">collection: The collection from which the elements are copied.</param> 
        /// <exception cref="System.ArgumentNullException">The collection parameter cannot be null.</exception> 
        public ObservableRangeCollection(IEnumerable<T> collection)
            : base(collection) { }
    }

    public class BookFilter
    {
        public string Title { get; set; }
        public string Category { get; set; }
    }

    public class MainWindowModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public const string NO_CATEGORY = "(no category)";

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _bookCount;
        public string BookCount {  get { return _bookCount; } set { _bookCount = value; OnPropertyChanged("BookCount"); } }

        private string _message;
        public string Message { get { return _message; } set { _message = value; OnPropertyChanged("Message"); } }

        private bool _filterMode;
        public bool FilterMode { get { return _filterMode; } set { _filterMode = value; OnPropertyChanged("FilterMode"); } }

        public BookFilter Filter = new BookFilter { Title = "", Category = "" };

        public ObservableRangeCollection<Book> ShownBooks { get; set; }

        public HashSet<string> Sortings = new HashSet<string>();

        public List<Book> Books;

        public MainWindowModel()
        {
            ShownBooks = new ObservableRangeCollection<Book>();
            Books = new List<Book>();
            FilterMode = true;
        }

        public void LoadList(List<Book> list)
        {
            ShownBooks.Clear();
            list.ForEach(book => { book.DownloadedGUI = book.IsDownloaded; });
            ShownBooks.AddRange(list);
            BookCount = $"Shown: {ShownBooks.Count}";
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

        //сначала по категории, затем по названию
        public void ApplyFilter(string whatChanged)
        {
            var filteredList = new List<Book>();
            filteredList = (whatChanged == "" || filteredList.Count == 0 ? Books : ShownBooks.ToList()) ;

            if (!string.IsNullOrEmpty(Filter.Category) && Filter.Category != NO_CATEGORY)
            {
                filteredList = filteredList.FindAll(book => book.Category == Filter.Category).ToList();
            }
            
            if (!string.IsNullOrEmpty(Filter.Title))
            {
                var titleWords = Filter.Title.ToLower().Split(' ');
                filteredList = filteredList.FindAll(book => book.Title.ToLower().ContainsEvery(titleWords)).ToList();
            }

           /* if (filteredList.Count == Books.Count)
            {
                Sortings.Add("PostId");
                SortList("PostId", filteredList);
            }*/

            LoadList(filteredList);
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
                var cat = book.FirstCategory;
                if (!string.IsNullOrEmpty(cat))
                    if (!dict.ContainsKey(cat))
                        dict.Add(cat, 1);
                    else
                        dict[cat]++;
            }
            foreach (var add in File.ReadAllLines("../../settings/categories.txt"))
            {
                if (!string.IsNullOrEmpty(add) && !dict.ContainsKey(add))
                    dict.Add(add, 0);
            }
            var list = new List<string>();
            foreach (var kv in dict)
            {
                list.Add(kv.Key);
            }
            list.Add("(no category)");
            list.Sort();
            Book.Categories.Clear();
            foreach (var cat in list)
            {
                Book.Categories.Add(cat);
            }
        }

        public void SortList(string column, List<Book> _list = null)
        {
            var sortedList = _list ?? ShownBooks.ToList();
            
            switch (column)
            {
                case "PostId":
                if (GetSorting("PostId") < 0)
                    sortedList = sortedList.OrderByDescending(b => b.PostId).ToList();
                else
                    sortedList = sortedList.OrderBy(b => b.PostId).ToList();
                break;
                case "Title":
                if (GetSorting("Title") < 0)
                    sortedList = sortedList.OrderByDescending(b => b.Title).ToList();
                else
                    sortedList = sortedList.OrderBy(b => b.Title).ToList();
                break;
                case "Year":
                if (GetSorting("Year") < 0)
                    sortedList = sortedList.OrderByDescending(b => b.Year*1000000 + b.PostId).ToList();
                else
                    sortedList = sortedList.OrderBy(b => b.Year*1000000 + b.PostId).ToList();
                break;
                case "Category":
                if (GetSorting("Category") < 0)
                    sortedList = sortedList.OrderByDescending(b => b.Category).ToList();
                else
                    sortedList = sortedList.OrderBy(b => b.Category).ToList();
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

        public int SuggestCategories()
        {
            var result = 0;
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
                                result++;
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
