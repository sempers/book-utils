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
using AllItEbooksCrawler;
using CommonUtils;
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
       

        public MainWindow()
        {
            InitializeComponent();
            //Book.ROOT = BOOKS_ROOT;
            //model.SETTINGS_PATH = SETTINGS_PATH;

            if (!File.Exists(CommonData.GOOGLE_DRIVE_DB_PATH))
            {
                MessageBox.Show(@"Google drive db path (D:\books\google_drive\itdb\books.db does not exist!");
                Utils.CreateDirectory(Path.GetDirectoryName(CommonData.GOOGLE_DRIVE_DB_PATH));
            }

            if (!Directory.Exists(CommonData.BOOKS_ROOT))
            {
                Utils.CreateDirectory(CommonData.BOOKS_ROOT);
            }


            if (!File.Exists(CommonData.DB_PATH))
            {
                RestoreDB(null, null);
            }
            else if (File.Exists(CommonData.GOOGLE_DRIVE_DB_PATH))
            {
                var fi_backup = new FileInfo(CommonData.GOOGLE_DRIVE_DB_PATH);
                var fi_db_path = new FileInfo(CommonData.DB_PATH);
                if (fi_backup.LastWriteTime.Ticks > fi_db_path.LastWriteTime.Ticks)
                {
                    RestoreDB(null, null);
                }
                else
                {
                    InitList();
                }
            }
            else
            {
                InitList();
            }
        }

        /// <summary>
        /// Initialize book list
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
        /// Load books from DB
        /// </summary>
        /// <param name="applyFilter"></param>
        private void LoadBooksFromDb()
        {
            var list = db.LoadBooksFromDb();
            CommonData.LoadHidden();
            int count = list.RemoveAll(book => CommonData.HiddenIncludes(book));
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
            var lastUpdate = (new FileInfo(CommonData.DB_PATH)).LastWriteTime.ToString("dd.MM.yyyy HH:mm:ss");
            Notify($"Books loaded ok. DB last updated {lastUpdate}. Total {downloaded} books downloaded. {(syncNotDownloaded > 0 ? $"{syncNotDownloaded} books to synchronize." : "")}");
        }

        private void Notify(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        /// <summary>
        /// Download book files (pdf/epub) from allitebooks.org
        /// </summary>
        /// <returns></returns>
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
                            Utils.CreateDirectory(Path.GetDirectoryName(book.LocalPath));
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
                                Utils.FileCopy(book.DownloadUrl, book.LocalPath);
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
            CommonData.LoadCorrections();
            int booksAdded = await db.UpdateDbFromWeb();
            LoadBooksFromDb();
            Notify($"Books updated from the web, added {booksAdded} new books.");
        }

        private void _ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var book = ((FrameworkElement)e.OriginalSource).DataContext as Book;
            if (book != null)
            {
                CommonData.LoadCorrections();
                var bookWindow = new BookWindow(book, db, "EDIT");
                bookWindow.Owner = this;
                var result = bookWindow.ShowDialog();
                if (result == true && db.LastAction == "REMOVE")
                {
                    LoadBooksFromDb();
                }
            }
        }

        private async void _btnDownload_Click(object sender, RoutedEventArgs e)
        {
            await DownloadBooksAsync();
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
            // Filter by category
            if (books.Count == 0)
            {
                if (catListBox.SelectedItem != null)
                {
                    model.Filter.Category = catListBox.SelectedItem.ToString();
                    model.ApplyFilterAndLoad("category");
                }
                return;
            }
            // Assign a category
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
            HandleKeyPress(e);
        }

        /// <summary>
        /// Handle a keyboard key pressed when selected a book/some books
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task HandleKeyPress(KeyEventArgs e)
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
                    if (book.Sync != 1)
                    {
                        book.Sync = 1;
                        db.Save();
                    }
                    if (!book.IsChecked)
                    {
                        book.IsChecked = true;
                    }
                    if (!book.IsDownloaded)
                    {
                        await DownloadBooksAsync();
                    }
                    OpenBook(book);
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
                OpenBook(book);
            }
        }

        /// <summary>
        /// Open a book
        /// </summary>
        /// <param name="book"></param>
        private void OpenBook(Book book)
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
                OpenProcess(book?.DownloadUrl);
            }
            else
            {
                OpenProcess(book?.LocalPath);
            }
        }

        /// <summary>
        /// Open a file/url
        /// </summary>
        /// <param name="uri"></param>
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

        /// <summary>
        /// Suggest a category (deprecated)
        /// </summary>
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
        /// Add a category
        /// </summary>
        private void AddCategory(string newCategory)
        {
            if (!string.IsNullOrEmpty(newCategory) && !CommonData.Categories.Contains(newCategory))
            {
                CommonData.AddCategory(newCategory);
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
        /// Add a new custom book
        /// </summary>
        private void AddBook()
        {
            var newBook = new Book
            {
                PostId = db.GetCustomPostId(),
                Title = "",
                Category = model.Filter.Category,
                DownloadUrl = @"D:\books\google_drive\itdb\extra_books\"
            };
            var bookWindow = new BookWindow(newBook, db);
            bookWindow.Owner = this;
            var result = bookWindow.ShowDialog();
            if (result.HasValue && result.Value)
            {
                LoadBooksFromDb();
            }
        }

        /// <summary>
        /// Backup DB to Google drive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackupDB(object sender, RoutedEventArgs e)
        {
            db.Save();
            Utils.FileCopy(CommonData.DB_PATH, CommonData.GOOGLE_DRIVE_DB_PATH);
            Notify("DB backed up");
        }

        /// <summary>
        /// Clear book list
        /// </summary>
        private void ClearList()
        {
            model.Books.Clear();
            model.ShownBooks.Clear();
        }

        /// <summary>
        /// Restore DB from Google drive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RestoreDB(object sender, RoutedEventArgs e)
        {
            Notify("DB restoring...");
            ClearList();
            db?.ClearFile();
            if (File.Exists(CommonData.GOOGLE_DRIVE_DB_PATH))
            {
                Utils.FileCopy(CommonData.GOOGLE_DRIVE_DB_PATH, CommonData.DB_PATH);
                InitList();
            }
        }

        /// <summary>
        /// Handle rating setting
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            AddCategory(catListBox.Text);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            BackupDB(null, null);
        }

        /// <summary>
        /// Different commands
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _cmdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_cmdText.Text.Length > 0)
            {
                switch (_cmdText.Text)
                {
                    case "epubs":
                        db.UpdateEpubsFromWeb(); break;
                    case "suggest":
                        Suggest(); break;
                    case "listcat":
                        model.ListCategories(); break;
                    case "catreport":
                        model.CatReport(); break;
                    case "correct":
                        CommonData.LoadCorrections();
                        db.Correct();
                        model.DeleteEmptyFolders(CommonData.BOOKS_ROOT);
                        LoadBooksFromDb();
                        model.ListCategories();
                        Notify("Corrections made. Books reloaded."); break;
                    case "open_categories":
                        Process.Start(CommonData.SETTINGS_PATH + "categories.txt"); break;
                    case "open_corrections":
                        Process.Start(CommonData.SETTINGS_PATH + "corrections.txt"); break;
                    case "open_hidden":
                        Process.Start(CommonData.SETTINGS_PATH + "hidden.txt"); break;
                    case "open_suggestions":
                        Process.Start(CommonData.SETTINGS_PATH + "suggestions.txt"); break;
                }
            }
        }

        private void _btnUnfilter_Click(object sender, RoutedEventArgs e)
        {
            model.UnfilterFlag = true;
            txtTitle.Text = "";
            catListBox.SelectedItem = "(no category)";
            chkOnlySync.IsChecked = false;
            model.ApplyFilterAndLoad("");
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            model.Filter.OnlySync = chkOnlySync.IsChecked.Value;
            model.ApplyFilterAndLoad("");
        }

        private void chkAuthors_Click(object sender, RoutedEventArgs e)
        {
            model.AuthorMode = chkAuthors.IsChecked.Value;
            model.ApplyFilterAndLoad("");
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ctxOpenBook(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItem != null && listView.SelectedItem is Book)
            {
                OpenBook(listView.SelectedItem as Book);
            }
        }

        private void ctxOpenFolder(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItem != null && listView.SelectedItem is Book && ((Book)listView.SelectedItem).IsDownloaded)
            {
                Process.Start(Path.GetDirectoryName(((Book)listView.SelectedItem).LocalPath));
            }
        }

        private void ListView_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
