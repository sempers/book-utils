using System;
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
using AllItEbooksCrawler;

namespace BookUtils
{
    public class MainWindowModel : INotifyPropertyChanged
    {
        public bool SuggestedFlag { get; set; } = false;
        public bool UnfilterFlag { get; set; } = false;

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

        private Dictionary<string, int> CategoriesStats = new Dictionary<string, int>();

        public void CatReport()
        {
            ListCategories();
            var list = CategoriesStats.OrderBy(kv => kv.Key).Select(kv => kv.Key.PadKey()+$"{kv.Value}".PadLeft(4,'.')).ToArray();
            File.WriteAllLines(Path.Combine(CommonData.SETTINGS_PATH, "catAZ.txt"), list);
            list = CategoriesStats.OrderByDescending(kv => kv.Value).Select(kv => kv.Key.PadKey() + $"{kv.Value}".PadLeft(4, '.')).ToArray();
            File.WriteAllLines(Path.Combine(CommonData.SETTINGS_PATH, "catMaxMin.txt"), list);
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
            foreach (var add in File.ReadAllLines(Path.Combine(Path.Combine(CommonData.SETTINGS_PATH, "categories.txt"))))
            {
                if (!string.IsNullOrEmpty(add) && !set.ContainsKey(add))
                    set.Add(add, 0);
            }
            CategoriesStats = set;
            var list = set.Keys.ToList();
            list.Add("(no category)");
            list.Sort();
            CommonData.Categories.Clear();
            CommonData.Categories.AddRange(list);
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
            CommonData.LoadSuggestions();
            foreach (var book in ShownBooks.Where(book => book.Approved == 0))
            {
                var titleWords = book.Title.Replace(",", "").Split(' ').ToList();
                foreach (var kvPair in CommonData.Suggestions)
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
            return result;
        }
    }
}
