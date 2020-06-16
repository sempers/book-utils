﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;

namespace BookUtils
{
    public static class Extensions
    {
        public static string PadKey(this string key)
        {
            var num = key.Count(c => c == '/');
            for (int i =0; i<num; i++ )
            {
                key = "   " + key;
            }
            return key.PadRight(60, '.');
        }

        public static bool StartsWithSlash(this string s, string pattern)
        {
            return s == pattern || s.StartsWith(pattern + "/");
        }

        public static bool ContainsEvery(this string s, string[] array)
        {
            bool result = true;
            foreach (string str in array)
            {
                if (string.IsNullOrEmpty(str))
                    continue;
                result = result && s.Contains(str.Trim());
                if (!result)
                    break;
            }
            return result;
        }

        public static bool ContainsAny(this string s, string[] array)
        {
            bool result = false;
            foreach (string str in array)
            {
                if (string.IsNullOrEmpty(str))
                    continue;
                result = result || s.Contains(str.Trim());
                if (result)
                    break;
            }
            return result;
        }
    }

    public class RatingToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value.ToString())
            {
                case "0": return " ";
                case "1": return "💩";
                case "2": return "😑";
                case "3": return "👍";
                case "4": return "❤️";
                default: return " ";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
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
        public bool OnlySync { get; set; }
    }

    public class MainWindowModel : INotifyPropertyChanged
    {
        public bool SuggestedFlag { get; set; } = false;
        public bool UnfilterFlag { get; set; } = false;
        public event PropertyChangedEventHandler PropertyChanged;
        public const string NO_CATEGORY = "(no category)";

        public string SETTINGS_PATH;
               
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

        public bool AuthorMode;

        public BookFilter Filter = new BookFilter { Title = "", Category = "" };

        public ObservableRangeCollection<Book> ShownBooks { get; set; }

        public Dictionary<string, int> Sortings = new Dictionary<string, int>();

        public List<Book> Books;

        public MainWindowModel()
        {
            ShownBooks = new ObservableRangeCollection<Book>();
            Books = new List<Book>();
            FilterMode = true;
        }

        public void LoadList(List<Book> list)
        {
            if (list == null)
                return;
            ShownBooks.Clear();
            list.ForEach(book => { book.DownloadedGUI = book.IsDownloaded; });
            ShownBooks.AddRange(list);
            BookCount = $"Shown: {ShownBooks.Count}";
        }

        public int GetSorting(string column)
        {
            if (Sortings.ContainsKey(column))
            {
                Sortings[column] *= -1;                
            }
            else
            {
                Sortings[column] = 1;               
            }
            return Sortings[column];
        }

        //сначала по категории, затем по названию
        public void ApplyFilterAndLoad(string whatChanged)
        {
            var filteredList = Books;
            if (!UnfilterFlag)
            {
                //category
                if (!string.IsNullOrEmpty(Filter.Category) && Filter.Category != NO_CATEGORY)
                {
                    filteredList = filteredList.FindAll(book => book.Category != null && book.Category.StartsWithSlash(Filter.Category));
                }
                //title
                if (!string.IsNullOrEmpty(Filter.Title))
                {
                    var title = Filter.Title.ToLower();
                    //Authors
                    var authorMode = AuthorMode;
                    if (title.Contains("|"))
                    {
                        var titleWords = title.Split('|');
                        filteredList = authorMode
                            ? filteredList.FindAll(book => book.Authors.ToLower().ContainsAny(titleWords))
                            : filteredList.FindAll(book => book.Title.ToLower().ContainsAny(titleWords));
                    }
                    else
                    {
                        var titleWords = Filter.Title.ToLower().Split(' ');
                        filteredList = authorMode
                            ? filteredList.FindAll(book => book.Authors.ToLower().ContainsEvery(titleWords))
                            : filteredList.FindAll(book => book.Title.ToLower().ContainsEvery(titleWords));
                    }
                }
                //sync
                if (Filter.OnlySync)
                {
                    filteredList = filteredList.FindAll(book => book.Sync == 1);
                }
            } else
            {
                UnfilterFlag = false; 
            }
            //sort
            IEnumerable<Book> sortedList = null;
            switch (whatChanged) {
                case "category":
                    sortedList = filteredList.OrderBy(book => book.Category).ThenByDescending(book => book.PostId); break;
                case "title":
                case "":
                    sortedList = filteredList.OrderByDescending(book => book.PostId); break;
            }
            LoadList(sortedList.ToList());
        }

        public Dictionary<string, string> TxtCorrections { get; set; }
        public List<string> TxtHidden { get; set; }

        public void LoadCorrections()
        {
            if (File.Exists(Path.Combine(SETTINGS_PATH, "corrections.txt")))
            {
                TxtCorrections = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(Path.Combine(SETTINGS_PATH, "corrections.txt")))
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
            if (File.Exists(Path.Combine(SETTINGS_PATH, "hidden.txt")))
            {
                TxtHidden = File.ReadAllLines(Path.Combine(SETTINGS_PATH, "hidden.txt")).ToList();
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

        private Dictionary<string, int> CategoriesStats = new Dictionary<string, int>();

        public void CatReport()
        {
            ListCategories();
            var list = CategoriesStats.OrderBy(kv => kv.Key).Select(kv => kv.Key.PadKey()+$"{kv.Value}".PadLeft(4,'.')).ToArray();
            File.WriteAllLines(Path.Combine(SETTINGS_PATH, "catAZ.txt"), list);
            list = CategoriesStats.OrderByDescending(kv => kv.Value).Select(kv => kv.Key.PadKey() + $"{kv.Value}".PadLeft(4, '.')).ToArray();
            File.WriteAllLines(Path.Combine(SETTINGS_PATH, "catMaxMin.txt"), list);
        }

        public void ListCategories()
        {
            var set = new Dictionary<string, int>();
            foreach (var book in Books)
            {
                if (!set.ContainsKey(book.FirstCategory)) {
                    var count = Books.Count(b => b.FirstCategory.StartsWithSlash(book.FirstCategory));
                    set.Add(book.FirstCategory, count);
                }
            } 
        
            foreach (var add in File.ReadAllLines(Path.Combine(Path.Combine(SETTINGS_PATH, "categories.txt"))))
            {
                if (!string.IsNullOrEmpty(add) && !set.ContainsKey(add))
                    set.Add(add, 0);
            }
            CategoriesStats = set;
            var list = set.Keys.ToList();
            list.Add("(no category)");
            list.Sort();
            Book.Categories.Clear();
            Book.Categories.AddRange(list);
        }

        public void SortList(string column, IEnumerable<Book> list = null)
        {
            var sortedList = list ?? ShownBooks;
            
            switch (column)
            {
                case "PostId":
                sortedList = GetSorting("PostId") < 0 ? sortedList.OrderByDescending(b => b.PostId) : sortedList.OrderBy(b => b.PostId);
                    break;
                case "Authors":
                    sortedList = GetSorting("Authors") < 0 ? sortedList.OrderByDescending(b => b.Authors) : sortedList.OrderBy(b => b.Authors);
                    break;
                case "Title":
                sortedList = GetSorting("Title") < 0 ? sortedList.OrderByDescending(b => b.Title) : sortedList.OrderBy(b => b.Title);
                    break;
                case "Year":
                sortedList = GetSorting("Year") < 0 ? sortedList.OrderByDescending(b => b.Year*1000000 + b.PostId) : sortedList.OrderBy(b => b.Year*1000000 + b.PostId);
                    break;
                case "Category":
                sortedList = GetSorting("Category") < 0 ? sortedList.OrderByDescending(b => b.Category) : sortedList.OrderBy(b => b.Category);
                    break;
                case "Pages":
                    sortedList = GetSorting("Pages") < 0 ? sortedList.OrderByDescending(b => b.Pages) : sortedList.OrderBy(b => b.Pages);
                    break;
            }
            LoadList(sortedList.ToList());
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
            if (File.Exists(Path.Combine(SETTINGS_PATH, "suggestions.txt")))
            {
                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(Path.Combine(SETTINGS_PATH, "suggestions.txt")))
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
