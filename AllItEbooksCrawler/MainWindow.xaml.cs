using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using HtmlAgilityPack;

namespace BookUtils
{
    public class BookCategory
    {
        public string Value { get; set; }
        public int Num { get; set;  }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Crawler crawler;

        MainWindowModel model = new MainWindowModel();

        public const string ROOT = @"D:\books\it";

        public bool Suggested { get; set; } = false;   

        public Dictionary<string, string> TxtCorrections { get; set; }
        public List<string> TxtHidden { get; set; }
        
        public MainWindow()
        {
            InitializeComponent();
            Book.root = ROOT;
            InitList();
        }

        private void InitList()
        {
            crawler = new Crawler();
            crawler.Notify += Crawler_Notified;
            DataContext = model;
            LoadBooksFromDb();
            ListCategories();
        }

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

        private bool HiddenIncludes(Book b)
        {
            foreach (var line in TxtHidden)
            {
                if (line.Contains("*") && b.FirstCategory.Contains(line.Replace("*", "")) || b.FirstCategory == line)
                    return true;
            }
            return false;
        }

        private void LoadBooksFromDb()
        {
            var list = crawler.GetFromDb();
            LoadHidden();
            int count = list.RemoveAll(book => HiddenIncludes(book));
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
                        crawler.SyncBook(book.Id);
                        downloadedNotSynced++;
                    }
                }
                else if (book.Sync > 0)
                {
                    book.IsChecked = true;
                    syncNotDownloaded++;
                }
            });
            model.Books = list;
            model.LoadList(list);
            model.Sortings.Add("PostId");
            model.SortList("PostId");
            Notify($"Books loaded ok. Total {downloaded} books downloaded. {(syncNotDownloaded > 0 ? $"{syncNotDownloaded} books to synchronize." : "")}");
        }

        public void MakeDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void ListCategories()
        {
            var dict = new Dictionary<string, int>();
            foreach (var book in model.ShownBooks)
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

        public void UnsuggestCategories()
        {
            foreach (var book in model.ShownBooks)
            {
                if (book.Suggested)
                {
                    book.Suggested = false;
                    book.Category = book.OldCategory;
                }
            }
            Notify("Suggestions unfiled.");
        }

        public void SuggestCategories()
        {
            if (File.Exists("../../settings/suggestions.txt")) {
                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines("../../settings/suggestions.txt"))
                {
                    var split = line.Split('=');
                    dict.Add(split[0], split[1]);
                }
                foreach (var book in model.ShownBooks.Where(book => book.Approved == 0))
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
                Notify("Suggestions for current list filed.");
            }
        }

        private void Notify(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async Task DownloadBooksAsync()
        {
            var booksToDownload = model.ShownBooks.Where(book => book.IsChecked && !string.IsNullOrEmpty(book.DownloadUrl) && !book.IsDownloaded).ToList();
            if (booksToDownload.Count == 0)
                return;
            Notify("Downloads initialized...");
            int count = 0;
            int total = booksToDownload.Count;
            foreach (var book in booksToDownload)
            {
                try
                {
                    count++;
                    if (book.Sync == 0)
                        crawler.SyncBook(book.Id);
                    Notify($"Downloading book {count}/{total}: {book.Title}");
                    using (var wc = new WebClient())
                    {
                        MakeDir(Path.GetDirectoryName(book.LocalPath));
                        if (!book.IsDownloaded)
                            await wc.DownloadFileTaskAsync(new Uri(book.DownloadUrl), book.LocalPath);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Exception while downloading {book.DownloadUrl}: {e.Message}");
                    continue;
                }
            }
            Application.Current.Dispatcher.Invoke(() => { model.Message = $"Downloads finished. Total {model.Books.Count(book => book.IsDownloaded)} books downloaded."; });
        }


        private void DeleteEmptyFolders(string folder = null)
        {
            folder = folder ?? ROOT;
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

        private string ClearNumberInCategory(string category)
        {
            var re = new Regex(@"\(\d+\)");
            return re.Replace(category, "");
        }

        private void Crawler_Notified(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            LoadCorrections();
            int booksAdded = await crawler.UpdateDbFromWeb(TxtCorrections);
            LoadBooksFromDb();
            Notify($"Books updated from the web, added {booksAdded} new books.");
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            
            var book = ((FrameworkElement)e.OriginalSource).DataContext as Book;
            if (book.Url != null)
            {
                var bookWindow = new BookWindow(book, crawler, "EDIT");
                bookWindow.Owner = this;
                var result = bookWindow.ShowDialog();
            }
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            await DownloadBooksAsync();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            LoadCorrections();
            crawler.Correct(TxtCorrections);
            DeleteEmptyFolders();
            LoadBooksFromDb();
            ListCategories();
            Notify("Corrections made. Books reloaded.");
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Book book = listView.SelectedItem as Book;
            if (book != null)
            {
                catListBox.SelectedValue = book.FirstCategory;
                model.FilterMode = false;
            } else
            {
                model.FilterMode = true;
            }
        }

        private void ListView_Click_1(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource as GridViewColumnHeader == null)
                return;
            var column = (e.OriginalSource as GridViewColumnHeader).Content.ToString();
            model.SortList(column);
        }

        private void catListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var books = listView.SelectedItems;
            // Фильтр по категории
            if (books.Count == 0)
            {
                if (catListBox.SelectedItem != null)
                    model.FilterListByCategory(catListBox.SelectedItem.ToString());
                return;
            }
            //Присвоение категории
            foreach (Book book in books)
            {
                if (catListBox.SelectedItem == null || book.FirstCategory == catListBox.SelectedItem.ToString())
                    continue;
                else
                {
                    var oldPath = book.IsDownloaded ? book.LocalPath : null;
                    book.Category = catListBox.SelectedItem.ToString();
                    if (book.Approved == 0) book.Approved = 1;
                    if (book.Suggested) book.Suggested = false;
                    crawler.ChangeCategory(book.Id, book.Category);
                    if (oldPath != null && oldPath != book.LocalPath)
                    {
                        book.AutoMove(oldPath);
                    }
                }
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space && e.Key != Key.Return && e.Key != Key.Back && e.Key != Key.F1 && e.Key != Key.F2)
                return;
            if (e.Key == Key.Space)                     //Make Checked and Synchronized
            {
                var books = listView.SelectedItems;
                foreach (Book book in books)
                {
                    if (book == null)
                        return;
                    book.IsChecked = !book.IsChecked;
                }
            }
            if (e.Key == Key.Return)                    //Make Approved
            {
                var books = listView.SelectedItems;
                foreach (Book book in books)
                {
                    if (book == null)
                        return;
                    book.Suggested = false; book.Approved = 1;
                    crawler.ChangeCategory(book.Id, book.Category);
                }
            }
            if (e.Key == Key.Back)                     //Unsuggest
            {
                var books = listView.SelectedItems;
                foreach (Book book in books)
                {
                    if (book == null)
                        return;
                    book.Suggested = false; book.Approved = 1; book.Category = book.OldCategory;
                    crawler.ChangeCategory(book.Id, book.Category);
                }
            }
            if (e.Key == Key.F1)                    //Old dblclick, now go to url
            {    
                var book = listView.SelectedItem as Book;
                if (book != null && book.Url != null)
                {
                    Process.Start(book.Url);
                }
            }
            if (e.Key == Key.F2)            //Open the file
            {
                var book = listView.SelectedItem as Book;
                if (book!=null && book.IsDownloaded)
                {
                    Process.Start(book.LocalPath);
                }
            }
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            var search = ((TextBox)e.Source).Text;
            if (!string.IsNullOrEmpty(search))
            {
                if (search.Length >= 3)
                    model.FilterListByTitle(search);
            }
            else
            {
                model.FilterListByTitle("");
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            listView.SelectedIndex = -1;
            model.FilterMode = true;
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (Suggested)
            {
                UnsuggestCategories();
            }
            else
            {
                SuggestCategories();
            }
            Suggested = !Suggested;
        }

        private void UpdateCategories()
        {
            var sorted = Book.Categories.OrderBy(x => x).ToList();
            Book.Categories.Clear();
            foreach (var s in sorted)
            {
                Book.Categories.Add(s);
            }
        }

        private void AddCategory()
        {
            if (!string.IsNullOrEmpty(catListBox.Text) && !Book.Categories.Contains(catListBox.Text))
            {
                var newCategory = catListBox.Text;
                Book.Categories.Add(newCategory);
                UpdateCategories();
                var book = listView.SelectedItem as Book;
                if (book != null && string.IsNullOrEmpty(book.Category))
                {
                    book.Category = newCategory;
                }
            }
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            AddCategory();
        }
        
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            ListCategories();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var book = (e.OriginalSource as CheckBox).DataContext as Book;
            if (book != null && !book.IsChecked && book.IsDownloaded)
            {
                if (MessageBox.Show($"Do you really want to unsync '{book.Title}' and delete the file?", "Warning", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    crawler.SyncBook(book.Id, 0);
                    try
                    {
                        File.Delete(book.LocalPath);
                    }
                    catch
                    {
                        
                    }
                    Notify($"Unsynced ok. Total {model.Books.Count(b => b.IsDownloaded)} books downloaded.");
                }
            }
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            Book newBook = new Book
            {
                PostId = crawler.GetCustomPostId(),
                Title = "New title"
            };
            BookWindow bookWindow = new BookWindow(newBook, crawler);
            bookWindow.Owner = this;
            var result = bookWindow.ShowDialog();
            if (result.HasValue && result.Value)
            {
                LoadBooksFromDb();
            }
        }

        private async void UploadDB(object sender, RoutedEventArgs e)
        {
            var url = new Uri("https://spbookserv.herokuapp.com/api/update-db");
            var file = @"..\..\data\books.db";
            var filename = "books.db";
            var contentType = "application/octet";
            using (var webClient = new WebClient())
            {
                try
                {
                    string boundary = "------------------------" + DateTime.Now.Ticks.ToString("x");
                    webClient.Headers.Add("Content-Type", "multipart/form-data; boundary=" + boundary);
                    var fileData = webClient.Encoding.GetString(File.ReadAllBytes(file));
                    var package = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"db\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n{3}\r\n--{0}--\r\n", boundary, filename, contentType, fileData);

                    var nfile = webClient.Encoding.GetBytes(package);

                    byte[] resp = await webClient.UploadDataTaskAsync(url, "POST", nfile);
                    Notify("DB backed up.");
                }
                catch (Exception err) {
                    MessageBox.Show($"DB Backup error: {err.Message}");
                }                
            }
        }

        private void ClearList()
        {
            model.Books.Clear();
            model.ShownBooks.Clear();
        }

        private void DownloadDB(object sender, RoutedEventArgs e)
        {
            ClearList();
            crawler.ClearFile();
            DownloadDBAsync();
        }

        private async void DownloadDBAsync()
        {
            using (var wc = new WebClient())
            {
                await wc.DownloadFileTaskAsync("http://spbookserv.herokuapp.com/itdb/books.db", @"..\..\data\books.db");
                InitList();                
            }
        }
    }
}
