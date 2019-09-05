using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
        public int Num { get; set; }
    }

    public class RatingToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value.ToString())
            {
                case "0": return " ";
                case "1": return " *";
                case "2": return "❤️";
                case "3": return "💩";
                default: return " ";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AppDbCrawler crawler;

        MainWindowModel model = new MainWindowModel();

        public const string BOOKS_ROOT = @"D:\books\it";

        public const string GOOGLE_DRIVE_DB_PATH = @"D:\books\google_drive\itdb\books.db";

        public const string DB_PATH = @"..\..\data\books.db";

        public bool Suggested { get; set; } = false;

       
        public MainWindow()
        {
            InitializeComponent();
            Book.root = BOOKS_ROOT;
            if (!File.Exists(DB_PATH))
            {
                DownloadDBAsync();
            }
            else
            {
                InitList();
            }
        }

        private void InitList()
        {
            crawler = new AppDbCrawler();
            crawler.Notify += _crawler_Notified;
            DataContext = model;
            LoadBooksFromDb();
            model.ListCategories();
        }

        private void LoadBooksFromDb()
        {
            var list = crawler.GetFromDb();
            model.LoadHidden();
            int count = list.RemoveAll(book => model.HiddenIncludes(book));
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
                        crawler.Save();
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
                    {
                        book.Sync = 1;
                        crawler.Save();
                    }

                    Notify($"Downloading book {count}/{total}: {book.Title}");
                    if (book.DownloadUrl.StartsWith("http"))
                    {
                        using (var wc = new WebClient())
                        {
                            MakeDir(Path.GetDirectoryName(book.LocalPath));
                            if (!book.IsDownloaded)
                                await wc.DownloadFileTaskAsync(new Uri(book.DownloadUrl), book.LocalPath);
                        }
                    }
                    else
                    {
                        if (File.Exists(book.DownloadUrl))
                        {
                            MakeDir(Path.GetDirectoryName(book.LocalPath));
                            if (!book.IsDownloaded)
                                File.Copy(book.DownloadUrl, book.LocalPath);
                        }
                    }

                }
                catch (Exception e)
                {
                    MessageBox.Show($"Exception while downloading {book.DownloadUrl}: {e.Message}");
                    continue;
                }
            }
            Notify($"Downloads finished. Total {model.Books.Count(book => book.IsDownloaded)} books downloaded.");
        }


        private void DeleteEmptyFolders(string folder = null)
        {
            folder = folder ?? BOOKS_ROOT;
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

        private void _crawler_Notified(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async void _btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            model.LoadCorrections();
            int booksAdded = await crawler.UpdateDbFromWeb(model.TxtCorrections);
            LoadBooksFromDb();
            Notify($"Books updated from the web, added {booksAdded} new books.");
        }

        private void _ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var book = ((FrameworkElement)e.OriginalSource).DataContext as Book;
            if (book != null)
            {
                var bookWindow = new BookWindow(book, crawler, "EDIT");
                bookWindow.Owner = this;
                var result = bookWindow.ShowDialog();
            }
        }

        private async void _btnDownload_Click(object sender, RoutedEventArgs e)
        {
            await DownloadBooksAsync();
        }

        private void _btnCorrect_Click(object sender, RoutedEventArgs e)
        {
            model.LoadCorrections();
            crawler.Correct(model.TxtCorrections);
            DeleteEmptyFolders();
            LoadBooksFromDb();
            model.ListCategories();
            Notify("Corrections made. Books reloaded.");
        }

        private void _ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var book = listView.SelectedItem as Book;
            if (book != null)
            {
                catListBox.SelectedValue = book.FirstCategory;
                model.FilterMode = false;
            }
            else
            {
                model.FilterMode = true;
            }
        }

        private void _ListView_Click_1(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource as GridViewColumnHeader == null)
                return;
            var column = (e.OriginalSource as GridViewColumnHeader).Content.ToString();
            model.SortList(column);
        }

        private void _catListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                    //crawler.UpdateCategory(book.Id, book.Category);
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
                    //crawler.UpdateCategory(book.Id, book.Category);
                    crawler.Save();
                }
            }
            if (e.Key == Key.Back)                     //Unsuggest
            {
                var books = listView.SelectedItems;
                foreach (Book book in books)
                {
                    if (book == null)
                        return;
                    book.Suggested = false;
                    book.Approved = 1;
                    book.Category = book.OldCategory;
                    crawler.Save();
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
                if (book != null && book.IsDownloaded)
                {
                    Process.Start(book.LocalPath);
                }
            }
        }

        private void _TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            var search = ((TextBox)e.Source).Text;
            if (!string.IsNullOrEmpty(search))
            {
                if (search.Length >= 3)
                {
                    model.FilterListByTitle(search);
                    model.Filter.Title = search;
                }
            }
            else
            {
                model.FilterListByTitle("");
                model.Filter.Title = "";
            }
        }

        private void _btnFilterMode_Click(object sender, RoutedEventArgs e)
        {
            listView.SelectedIndex = -1;
            model.FilterMode = true;
        }

        private void _btnSuggest_Click(object sender, RoutedEventArgs e)
        {
            if (Suggested)
            {
                if (model.UnsuggestCategories())
                    Notify("Suggestions unfiled.");
            }
            else
            {
                if (model.SuggestCategories())
                    Notify("Suggestions for current list filed.");
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


        private void _checkBox_Click(object sender, RoutedEventArgs e)
        {
            var book = (e.OriginalSource as CheckBox).DataContext as Book;
            if (book != null && !book.IsChecked && book.IsDownloaded)
            {
                if (MessageBox.Show($"Do you really want to unsync '{book.Title}' and delete the file?", "Warning", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    book.Sync = 0;
                    crawler.Save();
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

        private void _btnAddBook_Click(object sender, RoutedEventArgs e)
        {
            AddBook(); 
        }

        private void AddBook()
        {
            var newBook = new Book
            {
                PostId = crawler.GetCustomPostId(),
                Title = "",
                DownloadUrl = @"D:\books\google_drive\itdb\extra_books\"
            };
            var bookWindow = new BookWindow(newBook, crawler);
            bookWindow.Owner = this;
            var result = bookWindow.ShowDialog();
            if (result.HasValue && result.Value)
            {
                LoadBooksFromDb();
                model.ApplyFilter();
            }
        }

        private void BackupDB(object sender, RoutedEventArgs e)
        {
            /* var url = new Uri("https://spbookserv.herokuapp.com/api/update-db");
            var file = DB_PATH;
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
            */
            /*const FTP = "ftp://sempers:huibhean1@host2.bakop.com/";
            using (var webClient = new WebClient()) {
                try {
                var resp = await webClient.UploadFileTaskAsync($"{FTP}/books.db", DB_PATH);
                Notify("DB backed up.");
                }
                catch (Exception e) {
                    MessageBox.Show($"DB Backup error: {err.Message}");
                }
            }*/
            File.Copy(DB_PATH, GOOGLE_DRIVE_DB_PATH, true);
            Notify("DB backed up");
        }

        private void ClearList()
        {
            model.Books.Clear();
            model.ShownBooks.Clear();
        }

        private void RestoreDB(object sender, RoutedEventArgs e)
        {
            ClearList();
            crawler.ClearFile();
            File.Copy(GOOGLE_DRIVE_DB_PATH, DB_PATH, true);
            InitList();
        }

        private void DownloadDBAsync()
        {
            /*using (var wc = new WebClient())
            {
                await wc.DownloadFileTaskAsync("http://spbookserv.herokuapp.com/itdb/books.db", DB_PATH);
                InitList();                
            }*/
            /*using (var wc = new WebClient())
            {
                await wc.DownloadFileTaskAsync("ftp://sempers:huibhean1@host2.bakop.com/books.db", DB_PATH);
                InitList();
            }*/
        }

        private void _TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var book = (sender as TextBlock).DataContext as Book;
            if (book != null)
            {
                if (book.Rating == 3)
                    book.Rating = 0;
                else
                    book.Rating++;
                crawler.Save();
            }
        }

        private void _btnListCategories(object sender, RoutedEventArgs e)
        {
            model.ListCategories();
        }

        private void _btnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            AddCategory();
        }
    }
}
