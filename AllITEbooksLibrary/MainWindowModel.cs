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
using BookUtils;
using OpenQA.Selenium.Chrome;
using AllITEbooksLib.Properties;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using HtmlAgilityPack;
using System.Net;
using System.Windows;
using CommonUtils;

namespace BookUtils
{
    public class MainWindowModel : INotifyPropertyChanged
    {
        AppDb db;

        public event NotifyHandler OnNotify;

        public bool SuggestedFlag { get; set; } = false;
        public bool UnfilterFlag { get; set; } = false;

        public List<Book> BackList { get; set; } = new List<Book>();

        public event PropertyChangedEventHandler PropertyChanged;

        public const string NO_CATEGORY = "(no category)";

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _bookCount;
        public string BookCount { get { return _bookCount; } set { _bookCount = value; OnPropertyChanged("BookCount"); } }

        private string _message;
        public string Message { get { return _message; } set { _message = value; OnPropertyChanged("Message"); } }

        private bool _filterMode;
        public bool FilterMode { get { return _filterMode; } set { _filterMode = value; OnPropertyChanged("FilterMode"); } }

        private bool _isORG;
        public bool IsORG { get => _isORG; set { _isORG = value; _isIN = !value; BookCommonData.SOURCE = _isORG ? "ORG" : "IN"; OnPropertyChanged("IsORG"); OnPropertyChanged("IsIN"); } }

        private bool _isIN;
        public bool IsIN { get => _isIN; set { _isORG = !value; _isIN = value; BookCommonData.SOURCE = _isORG ? "ORG" : "IN"; OnPropertyChanged("IsORG"); OnPropertyChanged("IsIN"); } }

        public bool AuthorMode;

        public BookFilter Filter = new BookFilter { Title = "", Category = "" };

        public ObservableRangeCollection<Book> ShownBooks { get; set; }

        public Dictionary<string, int> SortOrders = new Dictionary<string, int>();

        public List<Book> Books;

        public BookActions LastAction { get => db.LastAction; }

        public AppDb Db { get => db; }

        public MainWindowModel()
        {
            ShownBooks = new ObservableRangeCollection<Book>();
            Books = new List<Book>();
            FilterMode = true;

        }

        public void Init(NotifyHandler notifyHandler)
        {
            db = new AppDb(notifyHandler);
            OnNotify += notifyHandler;
            IsORG = true;
        }

        public async Task<int> UpdateDbFromWeb()
        {
            if (BookCommonData.SOURCE == "IN")
                return await db.UpdateDbFromWeb_IN();
            else
                return await db.UpdateDbFromWeb_ORG();
        }

        public void SaveBackList()
        {
            if (ShownBooks != null && ShownBooks.Count > 0)
            {
                BackList.Clear();
                BackList.AddRange(ShownBooks);
            }
        }

        public void GoBack()
        {
            if (BackList != null && BackList.Count > 0)
            {
                var tempBackList = new List<Book>();
                tempBackList.AddRange(BackList);
                SaveBackList();
                ShownBooks.Clear();
                ShownBooks.AddRange(tempBackList);
            }
        }

        public void LoadList(List<Book> list, bool noSavingBack = false)
        {
            if (list == null)
                return;
            if (!noSavingBack)
                SaveBackList();
            ShownBooks.Clear();
            list.ForEach(book => { book.DownloadedGUI = book.IsDownloaded; });
            ShownBooks.AddRange(list);
            BookCount = $"Shown: {ShownBooks.Count}";
        }

        public int GetOrder(string column)
        {
            if (SortOrders.ContainsKey(column))
            {
                SortOrders[column] *= -1;
            }
            else
            {
                SortOrders[column] = 1;
            }
            return SortOrders[column];
        }

        //сначала по категории, затем по названию
        public void ApplyFilterAndLoad(string whatChanged)
        {
            var list = Books;
            if (!UnfilterFlag)
            {
                //category
                if (!string.IsNullOrEmpty(Filter.Category) && Filter.Category != NO_CATEGORY)
                {
                    list = list.FindAll(book => book.Category != null && book.Category.FindCategory(Filter.Category));
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
                        list = authorMode
                            ? list.FindAll(book => book.Authors.ToLower().ContainsAny(titleWords))
                            : list.FindAll(book => book.Title.ToLower().ContainsAny(titleWords));
                    }
                    else
                    {
                        var titleWords = Filter.Title.ToLower().Split(' ');
                        list = authorMode
                            ? list.FindAll(book => book.Authors.ToLower().ContainsEvery(titleWords))
                            : list.FindAll(book => book.Title.ToLower().ContainsEvery(titleWords));
                    }
                }
                //sync
                if (Filter.OnlySync)
                {
                    list = list.FindAll(book => book.Sync == 1);
                }
            }
            else
            {
                UnfilterFlag = false;
            }
            //sort
            IEnumerable<Book> sortedList = null;
            switch (whatChanged)
            {
                case "category":
                    sortedList = list.OrderBy(book => book.Category).ThenByDescending(book => book.PostId); break;
                case "title":
                case "":
                    sortedList = list.OrderByDescending(book => book.PostId); break;
            }
            LoadList(sortedList.ToList());
        }

        private SortedDictionary<string, int> CategoriesStats = new SortedDictionary<string, int>();

        public void CatReport()
        {
            ListCategories();
            var list = CategoriesStats.OrderBy(kv => kv.Key).Select(kv => kv.Key.PadKey() + $"{kv.Value}".PadLeft(4, '.')).ToArray();
            File.WriteAllLines(Path.Combine(BookCommonData.SETTINGS_PATH, "catAZ.txt"), list);
            list = CategoriesStats.OrderByDescending(kv => kv.Value).Select(kv => kv.Key.PadKey() + $"{kv.Value}".PadLeft(4, '.')).ToArray();
            File.WriteAllLines(Path.Combine(BookCommonData.SETTINGS_PATH, "catMaxMin.txt"), list);
        }

        public void ListCategories()
        {
            var set = new SortedDictionary<string, int>();
            foreach (var book in Books)
            {
                if (!set.ContainsKey(book.FirstCategory))
                {
                    var count = Books.Count(b => b.FirstCategory.FindCategory(book.FirstCategory));
                    set.Add(book.FirstCategory, count);
                }
            }
            foreach (var addition in File.ReadAllLines(Path.Combine(Path.Combine(BookCommonData.SETTINGS_PATH, "categories.txt"))))
            {
                if (!string.IsNullOrEmpty(addition) && !set.ContainsKey(addition))
                    set.Add(addition, 0);
            }
            CategoriesStats = set;
            var list = set.Keys.ToList();
            list.Add(NO_CATEGORY);
            list.Sort();
            BookCommonData.Categories.Clear();
            BookCommonData.Categories.AddRange(list);
        }

        public void SortList(string column, IEnumerable<Book> list = null)
        {
            list = list ?? ShownBooks;

            switch (column)
            {
                case "PostId":
                    list = GetOrder("PostId") < 0 ? list.OrderByDescending(b => b.PostId) : list.OrderBy(b => b.PostId);
                    break;
                case "Authors":
                    list = GetOrder("Authors") < 0 ? list.OrderByDescending(b => b.Authors) : list.OrderBy(b => b.Authors);
                    break;
                case "Title":
                    list = GetOrder("Title") < 0 ? list.OrderByDescending(b => b.Title) : list.OrderBy(b => b.Title);
                    break;
                case "Year":
                    list = GetOrder("Year") < 0 ? list.OrderByDescending(b => b.Year * 1000000 + b.PostId) : list.OrderBy(b => b.Year * 1000000 + b.PostId);
                    break;
                case "Category":
                    list = GetOrder("Category") < 0 ? list.OrderByDescending(b => b.Category) : list.OrderBy(b => b.Category);
                    break;
                case "Pages":
                    list = GetOrder("Pages") < 0 ? list.OrderByDescending(b => b.Pages) : list.OrderBy(b => b.Pages);
                    break;
                case "PUB":
                    list = GetOrder("Pages") < 0 ? list.OrderByDescending(b => b.Publisher) : list.OrderBy(b => b.Publisher);
                    break;
            }
            LoadList(list.ToList(), noSavingBack: true);
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
            BookCommonData.LoadSuggestions();
            foreach (var book in ShownBooks.Where(book => book.Approved == 0))
            {
                var titleWords = book.Title.Replace(",", "").Split(' ').ToList();
                foreach (var kvPair in BookCommonData.Suggestions)
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

        /// <summary>
        /// Open a book
        /// </summary>
        /// <param name="book"></param>
        public void OpenBook(Book book)
        {
            if (book == null || !book.IsDownloaded)
                return;
            if (book.Rating == 0)
            {
                book.Rating = 2;
                db.Save();
            }
            if (book.PostId < 0)
            {
                BookCommonData.OpenProcess(book?.DownloadUrl);
            }
            else
            {
                BookCommonData.OpenProcess(book?.LocalPath);
            }
        }

        /// <summary>
        /// Load books from DB
        /// </summary>
        /// <param name="applyFilter"></param>
        public void LoadBooksFromDb()
        {
            var list = db.LoadBooksFromDb();
            BookCommonData.LoadHidden();
            int count = list.RemoveAll(book => BookCommonData.HiddenIncludes(book));
            int syncNotDownloaded = 0;
            int downloadedNotSynced = 0;
            int downloaded = 0;
            list.ForEach(book =>
            {
                if (book.IsDownloaded)
                {
                    book.IsChecked = true;
                    downloaded++;
                    if (book.Sync == 0)     //downloaded but not synced
                    {
                        book.Sync = 1;
                        db.Save();
                        downloadedNotSynced++;
                    }
                }
                else if (book.Sync > 0)
                {
                    book.IsChecked = true;
                    syncNotDownloaded++;
                }
            });
            Books = list;
            GetOrder("PostId");
            ApplyFilterAndLoad("");
            var lastUpdate = (new FileInfo(BookCommonData.DB_PATH)).LastWriteTime.ToString("dd.MM.yyyy HH:mm:ss");
            OnNotify($"Books loaded ok. DB last updated {lastUpdate}. Total {downloaded} books downloaded. {(syncNotDownloaded > 0 ? $"{syncNotDownloaded} books to synchronize." : "")}");
        }

        public void Save()
        {
            db.Save();
        }

        /// <summary>
        /// Suggest a category (deprecated)
        /// </summary>
        public void Suggest()
        {
            if (SuggestedFlag)
            {
                if (UnsuggestCategories())
                {
                    OnNotify("Suggestions unfiled.");
                }
                SuggestedFlag = false;
            }
            else
            {
                var suggCount = SuggestCategories();
                if (suggCount > 0)
                {
                    OnNotify($"{suggCount} suggestions for current list filed.");
                    SuggestedFlag = true;
                }
            }
        }

        public void Correct()
        {
            BookCommonData.LoadCorrections();
            db.Correct();
            DeleteEmptyFolders(BookCommonData.BOOKS_ROOT);
            LoadBooksFromDb();
            ListCategories();
            OnNotify("Corrections made. Books reloaded.");
        }

        public async Task UpdateYear()
        {
            int c = 0;
            foreach (var book in Books.Where(b => b.Year < 1980 || b.Year > 2020))
            {
                if (book.DownloadUrl.StartsWith("http"))
                {
                    var year = book.DownloadUrl.Substring(book.DownloadUrl.IndexOf(".com/") + 5, 4);
                    book.Year = int.Parse(year);
                    c++;
                }
            }
            Save();
            OnNotify($"{c} books were corrected by Year field");
        }

        

        public void ResetEdition()
        {
            foreach (var book in Books)
            {
                book.Edition = 1;
                book.Obsolete = 0;
                for (var i = 0; i < BookCommonData.EDITION_TEXTS.Length/2; i++)
                {
                    if (book.Title.Contains(BookCommonData.EDITION_TEXTS[i, 0]) || book.Title.Contains(BookCommonData.EDITION_TEXTS[i, 1]))
                    {
                        book.Edition = i + 2;
                        break;
                    }
                }
            }
            Save();
        }

        public void MakeObsolete()
        {
            int counter = 0;
            foreach (var book in Books.Where(b=> b.Edition > 1))
            {
                try
                {
                    string titleWOEd = book.Title.Replace(BookCommonData.EDITION_TEXTS[book.Edition - 2, 0], "").Replace(BookCommonData.EDITION_TEXTS[book.Edition - 2, 1], "");
                    foreach (var book_old in Books.Where(b => b.Title.StartsWith(titleWOEd) && b.Edition < book.Edition))
                    {
                        book_old.Obsolete = 1;
                        counter++;
                    }
                }
                catch { }
            }
            Save();
            OnNotify($"{counter} books were made obsolete");
        }

        public async Task CheckEdition(int ed)
        {
            string[] SECOND_ED = new string[] { ", 2nd Edition", ", Second Edition" };
            string[] THIRD_ED = new string[] { ", 3rd Edition", ", Third Edition" };
            int counter = 0;
            if (ed == 3)
            {
                foreach (var book in Books.Where(b => b.Title.ContainsAny(THIRD_ED)))
                {
                    foreach (var book_old in Books.Where(b => b.Authors == book.Authors && b.PostId != book.PostId &&
                    (b.Title == book.Title.ReplaceAny(THIRD_ED, SECOND_ED[0]) || b.Title == book.Title.ReplaceAny(THIRD_ED, SECOND_ED[1]) || b.Title == book.Title.ReplaceAny(THIRD_ED, ""))))
                    {
                        book_old.Obsolete = 1; counter++;
                    }
                }
            }
            else if (ed == 2)
            {
                foreach (var book in Books.Where(b => b.Title.ContainsAny(SECOND_ED)))
                {
                    foreach (var book_old in Books.Where(b => b.Authors == book.Authors && b.PostId != book.PostId && (b.Title == book.Title.ReplaceAny(SECOND_ED, ""))))
                    {
                        book_old.Obsolete = 1; counter++;
                    }
                }
            }
            OnNotify($"{counter} books were adjusted.");
            Save();
        }

        public async Task Modify_IN_ID()
        {
            foreach (var book in Books.Where(b => b.PostId >= 33883))
            {
                book.PostId += 100000;
            }
            Save();
            OnNotify("Modification succeeded");
            LoadBooksFromDb();
        }

        public async Task UpdateCategories()
        {
            OnNotify("Searching for files...");
            var pathMap = new Dictionary<string, string>();
            var rootPath = Settings.Default.BooksRoot;

            List<string> DirSearch(string sDir)
            {
                var paths = new List<string>();
                try
                {
                    foreach (var f in Directory.GetFiles(sDir))
                    {
                        paths.Add(f);
                    }
                    foreach (var d in Directory.GetDirectories(sDir))
                    {
                        paths.AddRange(DirSearch(d));
                    }
                }
                catch (Exception e)
                {

                }
                return paths;
            }

            var allPaths = DirSearch(rootPath);
            foreach (var path in allPaths)
            {
                pathMap[Path.GetFileName(path)] = path;
            }
            int c = 0;
            foreach (Book b in ShownBooks.Where(b => b.Sync == 1 && !b.IsDownloaded))
            {
                if (pathMap.ContainsKey(b.ClearFileName))
                {
                    var trueCategory = pathMap[b.ClearFileName].Replace(rootPath + "\\", "").Replace("\\" + b.ClearFileName, "");
                    b.Category = trueCategory.Replace("\\", "/");
                    c++;
                }
            }
            Save();
            OnNotify($"Found and recategorized {c} books.");
        }

        public async Task UpdateISBN()
        {
            var i = 0;
            var list = ShownBooks.Where(b => string.IsNullOrEmpty(b.ISBN)).ToList();
            foreach (var book in list)
            {
                try
                {
                    i++;
                    var isbn = "";
                    if (BookCommonData.SOURCE == "IN")
                        isbn = (await db.UpdateBookFromWeb_IN(book, $"{i}/{list.Count}")).ISBN;
                    else
                        isbn = (await db.UpdateBookFromWeb_ORG(book, $"{i}/{list.Count}")).ISBN;
                    book.ISBN = isbn;
                    Save();
                    Thread.Sleep(250);
                }
                catch { }
            }
        }

        /// <summary>
        /// Clear book list
        /// </summary>
        public void ClearList()
        {
            Books.Clear();
            ShownBooks.Clear();
            BackList.Clear();
            db?.ClearDbFile();
        }

        /// <summary>
        /// Add a new custom book
        /// </summary>
        public Book AddBook()
        {
            return new Book
            {
                PostId = db.GetCustomPostId(),
                Title = "",
                Category = Filter.Category,
                DownloadUrl = Settings.Default.ExtraBooksPath
            };
        }


        public void UnsyncBook(Book book)
        {
            book.Sync = 0;
            Save();
            if (book.IsDownloaded)
            {
                CommonUtils.Utils.DeleteFile(book.LocalPath);
            }
            OnNotify($"Unsynced ok. Total {Books.Count(b => b.IsDownloaded)} books downloaded.");
        }

        public async Task<int> UpdateEpubsFromWeb()
        {
            return await db.UpdateEpubsFromWeb();
        }

        public ChromeDriver driver = null;


        /// <summary>
        /// Download book files (pdf/epub) from allitebooks.org
        /// </summary>
        /// <returns></returns>
        public async Task DownloadBooksAsync(System.Collections.IList books, Book paramBook = null)
        {
            var booksToDownload = new List<Book>();
            if (books == null)
                return;
            bool inSelection = books.Count > 0;
            if (paramBook != null)
            {
                booksToDownload.Add(paramBook);
            }
            else
            {
                if (!inSelection)
                {
                    booksToDownload = ShownBooks.Where(book => book.IsChecked && !string.IsNullOrEmpty(book.DownloadUrl) && !book.IsDownloaded).ToList();
                }
                else
                {
                    var selected = new List<Book>();
                    foreach (Book b in books)
                    {
                        selected.Add(b);
                    }
                    booksToDownload = selected.Where(book => book.IsChecked && !string.IsNullOrEmpty(book.DownloadUrl) && !book.IsDownloaded).ToList();
                }
                if (booksToDownload.Count == 0)
                    return;
            }
            if (booksToDownload.Count == 1)
            {
                paramBook = booksToDownload[0];
            }
            OnNotify(inSelection ? "Downloads in selection initialized..." : "Downloads initialized");
            int count = 0;
            int total = booksToDownload.Count;
            foreach (var book in booksToDownload)
            {
                try
                {
                    count++;
                    if (book.Sync == 0)
                    {
                        book.Sync = 1;
                        Save();
                    }
                    OnNotify($"Downloading book {count}/{total}: {book.Title}");
                    if (book.DownloadUrl.StartsWith("http"))
                    {
                        CommonUtils.Utils.CreateDirectory(Path.GetDirectoryName(book.LocalPath));
                        book.DownloadUrl = book.DownloadUrl.Trim();
                        //Вот тут новая заморочь
                        if (book.DownloadUrl.EndsWith("file.html") && !book.IsDownloaded && paramBook != null)
                        {
                            OnNotify($"Downloading book {count}/{total}: {book.Title}, starting Chrome Driver...");
                            if (driver == null)
                                driver = new ChromeDriver();
                            driver.Navigate().GoToUrl(book.DownloadUrl);
                            IWait<IWebDriver> wait = new WebDriverWait(driver, TimeSpan.FromSeconds(2.00));
                            wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
                            HtmlDocument page = new HtmlDocument();
                            page.LoadHtml(driver.PageSource);
                            var dlButton = page.DocumentNode.SelectSingleNode("//a[@id='dlbutton']");
                            var endHref = dlButton.Attributes["href"].Value;
                            var href = "https://" + new Uri(book.DownloadUrl).Host + endHref;
                            var rarFileName = Settings.Default.BooksRoot + "\\RAR\\" + href.Substring(href.LastIndexOf("/") + 1);
                            using (var wc = new WebClient())
                            {
                                OnNotify($"Downloading book {count}/{total}: {book.Title}, downloading the link...");
                                await wc.DownloadFileTaskAsync(new Uri(href), rarFileName);
                            }
                            OnNotify($"Downloading book {count}/{total}: {book.Title}, unraring...");
                            var finalPath = BookCommonData.UnrarFile(rarFileName);
                            book.Extension = Path.GetExtension(finalPath).Replace(".", "");
                            File.Move(finalPath, book.LocalPath);
                        }
                        else if (!book.IsDownloaded)
                        {
                            using (var wc = new WebClient())
                            {
                                await wc.DownloadFileTaskAsync(new Uri(book.DownloadUrl), book.LocalPath);
                            }
                            book.DownloadedGUI = true;
                        }
                    }
                    else
                    {
                        if (File.Exists(book.DownloadUrl))
                        {
                            if (!book.IsDownloaded)
                            {
                                CommonUtils.Utils.FileCopy(book.DownloadUrl, book.LocalPath);
                                book.DownloadedGUI = true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (paramBook != null)
                        MessageBox.Show($"Exception while downloading {book.DownloadUrl}: {e.Message}");
                    else
                        OnNotify($"Error while downloading {book.DownloadUrl}, proceeding...");
                    book.Sync = 0;
                    Save();
                    Utils.DeleteFile(book.LocalPath);
                    continue;
                }
            }
            OnNotify($"Downloads finished. Total {Books.Count(book => book.IsDownloaded)} books downloaded.");
        }
    }
}
