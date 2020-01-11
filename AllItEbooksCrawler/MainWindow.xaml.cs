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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AppDbCrawler db;

        MainWindowModel model = new MainWindowModel();

        public const string BOOKS_ROOT = @"D:\books\it";

        public const string GOOGLE_DRIVE_DB_PATH = @"D:\books\google_drive\itdb\books.db";

        public const string DB_PATH = @"..\..\data\books.db";      

       
        public MainWindow()
        {
            InitializeComponent();
            Book.ROOT = BOOKS_ROOT;
            if (!File.Exists(DB_PATH))
            {
                RestoreDB(null, null);
            }
            else if (File.Exists(GOOGLE_DRIVE_DB_PATH))
            {
                var fi_backup = new FileInfo(GOOGLE_DRIVE_DB_PATH);
                var fi_db_path = new FileInfo(DB_PATH);
                if (fi_backup.LastWriteTime.Ticks > fi_db_path.LastWriteTime.Ticks)
                {
                    RestoreDB(null, null);
                }
                else
                {
                    InitList();
                }
            } else
            {
                InitList();
            }
        }

        /// <summary>
        /// Инициализация основного списка
        /// </summary>
        private void InitList()
        {
            db = new AppDbCrawler();
            db.Notify += Notify;
            DataContext = model;
            LoadBooksFromDb();
            model.ListCategories();
        }

        /// <summary>
        /// Процедура загрузки из БД
        /// </summary>
        /// <param name="applyFilter"></param>
        private void LoadBooksFromDb(bool applyFilter = false)
        {
            var list = db.LoadBooksFromDb();
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
            model.Books = list;

            model.GetSorting("PostId");
            model.ApplyFilterAndLoad("");
            var lastUpdate = (new FileInfo(DB_PATH)).LastWriteTime.ToString("dd.MM.yyyy HH:mm:ss");
            Notify($"Books loaded ok. DB last updated {lastUpdate}. Total {downloaded} books downloaded. {(syncNotDownloaded > 0 ? $"{syncNotDownloaded} books to synchronize." : "")}");
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
            bool inSelection = listView.SelectedItems.Count > 0;
            List<Book> booksToDownload;
            if (!inSelection)
            {
                booksToDownload = model.ShownBooks.Where(book => book.IsChecked && !string.IsNullOrEmpty(book.DownloadUrl) && !book.IsDownloaded).ToList();
            }
            else
            {
                var selected = new List<Book>();
                foreach (Book b in listView.SelectedItems)
                {
                    selected.Add(b);
                }
                booksToDownload = selected.Where(book => book.IsChecked && !string.IsNullOrEmpty(book.DownloadUrl) && !book.IsDownloaded).ToList();
            }
            if (booksToDownload.Count == 0)
                return;
            
            Notify(inSelection ? "Downloads in selection initialized..." : "Downloads initialized");
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
                        db.Save();
                    }

                    Notify($"Downloading book {count}/{total}: {book.Title}");
                    if (book.DownloadUrl.StartsWith("http"))
                    {
                        using (var wc = new WebClient())
                        {
                            MakeDir(Path.GetDirectoryName(book.LocalPath));
                            if (!book.IsDownloaded)
                            {
                                await wc.DownloadFileTaskAsync(new Uri(book.DownloadUrl), book.LocalPath);
                                book.DownloadedGUI = true;
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(book.DownloadUrl))
                        {
                            if (!book.IsDownloaded)
                            {
                                MakeDir(Path.GetDirectoryName(book.LocalPath));
                                File.Copy(book.DownloadUrl, book.LocalPath);
                                book.DownloadedGUI = true;
                            }
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

        private async void _btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            model.LoadCorrections();
            int booksAdded = await db.UpdateDbFromWeb(model.TxtCorrections);
            LoadBooksFromDb();
            Notify($"Books updated from the web, added {booksAdded} new books.");
        }

        private void _ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var book = ((FrameworkElement)e.OriginalSource).DataContext as Book;
            if (book != null)
            {
                var bookWindow = new BookWindow(book, db, "EDIT");
                bookWindow.Owner = this;
                var result = bookWindow.ShowDialog();
                if (result == true && db.LastAction == "REMOVE")
                {
                    LoadBooksFromDb(applyFilter: true);
                }
            }
        }

        private async void _btnDownload_Click(object sender, RoutedEventArgs e)
        {
            await DownloadBooksAsync();
        }

        private void _btnCorrect_Click(object sender, RoutedEventArgs e)
        {
            model.LoadCorrections();
            db.Correct(model.TxtCorrections);
            model.DeleteEmptyFolders(BOOKS_ROOT);
            LoadBooksFromDb();
            model.ListCategories();
            Notify("Corrections made. Books reloaded.");
        }

        private void _ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var book = listView.SelectedItem as Book;
            if (book != null)
            {
                catListBox.SelectedValue = book.Category; //select current book's category
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
            if (model.UnfilterFlag)
            {
                model.Filter.Category = "";
                return;
            }

            var books = listView.SelectedItems;
            // Фильтр по категории
            if (books.Count == 0)
            {
                if (catListBox.SelectedItem != null)
                {
                    model.Filter.Category = catListBox.SelectedItem.ToString();
                    model.ApplyFilterAndLoad("category");
                }
                return;
            }
            //Присвоение категории
            foreach (Book book in books)
            {
                if (catListBox.SelectedItem != null && book.FirstCategory != catListBox.SelectedItem.ToString())
                {
                    book?.SetCategory(catListBox.SelectedItem.ToString(), approve: true);
                }
            }
        }

        private void _Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space && e.Key != Key.Return && e.Key != Key.Back && e.Key != Key.F1 && e.Key != Key.F2)
                return;
            if (e.Key == Key.Space)                     //Make Checked and Synchronized
            {
                foreach (Book book in listView.SelectedItems)
                {
                    book?.ToggleCheck();
                }
            }
            if (e.Key == Key.Return)                    //Make Approved/Open File
            {
                foreach (Book _book in listView.SelectedItems)
                {
                    if (_book != null)
                    {
                        _book.Suggested = false;
                        _book.Approved = 1;
                        db.Save();
                    }
                }
                var book = listView.SelectedItem as Book;
                if (book != null)
                {
                    if (book.IsDownloaded)
                    {
                        OpenProcess(book.LocalPath);
                        if (book.Rating == 0)
                        {
                            book.Rating = 2;
                            db.Save();
                        }
                    }
                    else
                    {
                        DownloadBooksAsync();
                    }
                }
            }
            if (e.Key == Key.Back)           //Unsuggest and approve initial value
            {
                foreach (Book book in listView.SelectedItems)
                {
                    if (book != null)
                    {
                        book.SetCategory(book.OldCategory, approve: true);
                        db.Save();
                    }
                }
            }
            if (e.Key == Key.F1)              //Old dblclick, now go to url
            {
                var book = listView.SelectedItem as Book;
                OpenProcess(book?.Url);
            }
            if (e.Key == Key.F2)            //Open the file
            {
                var book = listView.SelectedItem as Book;
                if (book != null && book.IsDownloaded)
                {
                    OpenProcess(book.LocalPath);
                }
            }
        }

        private void OpenProcess(string uri)
        {
            if (uri == null)
                return;
            try
            {
                Process.Start(uri);
            }
            catch (Exception e)
            {
                MessageBox.Show("Exception while opening " + e.Message);
            }
        }

        private void _txtTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (model.UnfilterFlag)
            {
                model.Filter.Title = "";
                return;
            }               

            var search = ((TextBox)e.Source).Text;
            if (!string.IsNullOrEmpty(search))
            {
                if (search.Length >= 3)
                {
                    model.Filter.Title = search;
                    model.ApplyFilterAndLoad("title");
                }
            }
            else
            {
                model.Filter.Title = "";
                model.ApplyFilterAndLoad("title");
            }
        }

        private void _btnFilterMode_Click(object sender, RoutedEventArgs e)
        {
            listView.SelectedIndex = -1;
            model.FilterMode = true;
        }

        private void Suggest()
        {
            if (model.SuggestedFlag)
            {
                if (model.UnsuggestCategories())
                {
                    Notify("Suggestions unfiled.");
                }
                model.SuggestedFlag = false;
            }
            else
            {
                var suggCount = model.SuggestCategories();
                if (suggCount > 0)
                {
                    Notify($"{suggCount} suggestions for current list filed.");
                    model.SuggestedFlag = true;
                }
            }
        }

        /// <summary>
        /// Добавляем категорию
        /// </summary>
        private void AddCategory()
        {
            var newCategory = catListBox.Text;
            if (!string.IsNullOrEmpty(newCategory) && !Book.Categories.Contains(newCategory))
            {
                Book.AddCategory(newCategory);
                var book = listView.SelectedItem as Book;
                book?.SetCategory(newCategory, approve: true); //setter logic
            }
        }

        private void _checkBox_Click(object sender, RoutedEventArgs e)
        {
            var book = (e.OriginalSource as CheckBox).DataContext as Book;
            if (book != null && !book.IsChecked)
            {
                UnsyncBook(book);
            }
        }

        private void UnsyncBook(Book book)
        {
            if (MessageBox.Show($"Do you really want to unsync '{book.Title}' and delete the file?", "Warning", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                book.Sync = 0;
                db.Save();
                if (book.IsDownloaded)
                {
                    try
                    {
                        File.Delete(book.LocalPath);
                    }
                    catch
                    {

                    }
                }
                Notify($"Unsynced ok. Total {model.Books.Count(b => b.IsDownloaded)} books downloaded.");
            }
        }

        private void _btnAddBook_Click(object sender, RoutedEventArgs e)
        {
            AddBook(); 
        }

        /// <summary>
        /// Добавление новой книги
        /// </summary>
        private void AddBook()
        {
            var newBook = new Book
            {
                PostId = db.GetCustomPostId(),
                Title = "",
                Category=model.Filter.Category,
                DownloadUrl = @"D:\books\google_drive\itdb\extra_books\"
            };
            var bookWindow = new BookWindow(newBook, db);
            bookWindow.Owner = this;
            var result = bookWindow.ShowDialog();
            if (result.HasValue && result.Value)
            {
                LoadBooksFromDb(applyFilter: true);
            }
        }

        private void BackupDB(object sender, RoutedEventArgs e)
        {
            db.Save();
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
            Notify("DB restoring...");
            ClearList();
            db?.ClearFile();
            if (File.Exists(GOOGLE_DRIVE_DB_PATH))
            {
                File.Copy(GOOGLE_DRIVE_DB_PATH, DB_PATH, true);
                InitList();
            }
        }

        private void _TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var book = (sender as TextBlock).DataContext as Book;
            if (book != null)
            {
                if (book.Rating == 4)
                    book.Rating = 0;
                else
                    book.Rating++;
                db.Save();
            }
        }

        private void _btnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            AddCategory();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            BackupDB(null, null);
        }

        private void _cmdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_cmdText.Text.Length>0)
            {
                switch (_cmdText.Text){
                    case "epubs":
                        db.UpdateEpubsFromWeb(); break;
                    case "suggest":
                        Suggest(); break;
                    case "listcat":
                        model.ListCategories(); break;
                    case "catreport":
                        model.CatReport(); break;
                }
            }
        }

        private void _btnUnfilter_Click(object sender, RoutedEventArgs e)
        {
            model.UnfilterFlag = true;
            txtTitle.Text = "";
            catListBox.SelectedItem = "(no category)";
            model.ApplyFilterAndLoad("");            
        }
    }
}
