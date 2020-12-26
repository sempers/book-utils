using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AllITEbooksLib.Properties;

namespace BookUtils
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowModel model = new MainWindowModel();

        public MainWindowModel Model { get => model; }

        public MainWindow()
        {
            var mutex = new Mutex(true, "BookUtilsMutex", out bool createdNew);
            if (!createdNew)
                Close();

            InitializeComponent();

            if (!File.Exists(BookCommonData.GOOGLE_DRIVE_DB_PATH) && Settings.Default.UseGoogleDrive)
            {
                MessageBox.Show($@"Google drive db path ({BookCommonData.GOOGLE_DRIVE_DB_PATH} does not exist!");
                CommonUtils.Utils.CreateDirectory(Path.GetDirectoryName(BookCommonData.GOOGLE_DRIVE_DB_PATH));
            }

            CommonUtils.Utils.CreateDirectory(BookCommonData.BOOKS_ROOT);

            if (!File.Exists(BookCommonData.DB_PATH))
            {
                if (!Settings.Default.UseGoogleDrive)
                {
                    MessageBox.Show("No books.db found! Check the settings!");
                    return;
                }
                RestoreDb();
            }
            else if (File.Exists(BookCommonData.GOOGLE_DRIVE_DB_PATH) && Settings.Default.UseGoogleDrive)
            {
                var fi_backup = new FileInfo(BookCommonData.GOOGLE_DRIVE_DB_PATH);
                var fi_db_path = new FileInfo(BookCommonData.DB_PATH);
                if (fi_backup.LastWriteTime.Ticks > fi_db_path.LastWriteTime.Ticks)
                {
                    // _btnRestore_Click(null, null);
                    RestoreDb();
                }
                else
                {
                    Init();
                }
            }
            else
            {
                Init();
            }
        }
               
        private void Notify(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        /// <summary>
        /// Initialize book list
        /// </summary>
        private void Init()
        {
            model.Init(Notify);
            DataContext = model;
            rbOrg.IsChecked = true;
            model.LoadBooksFromDb();
            model.ListCategories();
        }        

        private async void _btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            BookCommonData.LoadCorrections();
            int booksAdded = await model.UpdateDbFromWeb();
            model.LoadBooksFromDb();
            Notify($"Books updated from the web, added {booksAdded} new books.");
        }

        private void _ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var book = ((FrameworkElement)e.OriginalSource).DataContext as Book;
            if (book != null)
            {
                BookCommonData.LoadCorrections();
                var bookWindow = new BookWindow(book, this, BookActions.Edit);
                var result = bookWindow.ShowDialog();
                if (result == true && model.LastAction == BookActions.Remove)
                {
                    model.LoadBooksFromDb();
                }
            }
        }

        private async void _btnDownload_Click(object sender, RoutedEventArgs e)
        {
            await model.DownloadBooksAsync(listView.SelectedItems);
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
        private async void _Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space && e.Key != Key.Return && e.Key != Key.Back && e.Key != Key.F1 && e.Key != Key.F2 && e.Key != Key.Delete)
                return;

            if (e.Key == Key.Delete)
            {
                var book = listView.SelectedItem as Book;

                if (book != null && MessageBox.Show($"Do you really want to delete the book `{book.Title}`?", "Delete book", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    model.Db.RemoveBook(book);
                    model.LoadBooksFromDb();                       
                }               
            }

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
                        model.Save();
                    }
                }
                var book = listView.SelectedItem as Book;
                if (book != null)
                {
                    if (book.Sync != 1)
                    {
                        book.Sync = 1;
                        model.Save();
                    }
                    if (!book.IsChecked)
                    {
                        book.IsChecked = true;
                    }
                    if (!book.IsDownloaded)
                    {
                        await model.DownloadBooksAsync(null, book);
                    }
                    model.OpenBook(book);
                }
            }
            if (e.Key == Key.Back)           //Unsuggest and approve initial value
            {
                foreach (Book book in listView.SelectedItems)
                {
                    if (book != null)
                    {
                        book.SetCategory(book.OldCategory, approve: true);
                        model.Save();
                    }
                }
            }
            if (e.Key == Key.F1)              //Old dblclick, now go to url
            {
                var book = listView.SelectedItem as Book;
                BookCommonData.OpenProcess(book?.Url);
            }
            if (e.Key == Key.F2)            //Open the file
            {
                var book = listView.SelectedItem as Book;
                model.OpenBook(book);
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

        private void _checkBox_Click(object sender, RoutedEventArgs e)
        {
            var book = (e.OriginalSource as CheckBox).DataContext as Book;
            if (book != null && !book.IsChecked)
            {
                if (MessageBox.Show($"Do you really want to unsync '{book.Title}' and delete the file?", "Warning", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    model.UnsyncBook(book);
                }
            }
        }

        private void _btnAddBook_Click(object sender, RoutedEventArgs e)
        {
            var newBook  = model.AddBook();
            var bookWindow = new BookWindow(newBook, this)
            {
                Owner = this
            };

            var result = bookWindow.ShowDialog();
            if (result.Value == true)
            {
                model.LoadBooksFromDb();
            }
        }

        private void BackupDb()
        {
            if (Settings.Default.UseGoogleDrive)
            {
                model.Save();
                CommonUtils.Utils.FileCopy(BookCommonData.DB_PATH, BookCommonData.GOOGLE_DRIVE_DB_PATH);
                Notify("DB backed up");
            }
        }

        /// <summary>
        /// Backup DB to Google drive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _btnBackup_Click(object sender, RoutedEventArgs e)
        {
            BackupDb();
        }               
        
        private void _btnRestore_Click(object sender, RoutedEventArgs e)
        {
            RestoreDb();
        }

        /// <summary>
        /// Restore DB from Google drive
        /// </summary>
        private void RestoreDb()
        {
            if (Settings.Default.UseGoogleDrive)
            {
                Notify("DB restoring...");
                model.ClearList();
                if (File.Exists(BookCommonData.GOOGLE_DRIVE_DB_PATH))
                {
                    CommonUtils.Utils.FileCopy(BookCommonData.GOOGLE_DRIVE_DB_PATH, BookCommonData.DB_PATH);
                    Init();
                }
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
                model.Save();
            }
        }

        private void TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var book = (sender as TextBlock).DataContext as Book;
            if (book != null)
            {
                if (book.Read == 1)
                    book.Read = 0;
                else
                    book.Read = 1;
                model.Save();
            }
        }

        private void _btnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var newCategory = catListBox.Text;
            if (!string.IsNullOrEmpty(newCategory) && !BookCommonData.Categories.Contains(newCategory))
            {
                BookCommonData.AddCategory(newCategory);
                var book = listView.SelectedItem as Book;
                book?.SetCategory(newCategory, approve: true); //setter logic
                if (listView.SelectedItems.Count > 0)
                {
                    foreach (Book b in listView.SelectedItems)
                    {
                        b?.SetCategory(newCategory, approve: true);
                    }
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                model?.driver?.Close();
            }
            catch { }
            BackupDb();
        }

        /// <summary>
        /// Different commands
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _cmdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_cmdText.Text.Length > 0)
            {
                switch (_cmdText.Text)
                {
                    case "epubs":
                        await model.UpdateEpubsFromWeb(); break;
                    case "suggest":
                        model.Suggest(); break;
                    case "listcat":
                        model.ListCategories(); break;
                    case "catreport":
                        model.CatReport(); break;
                    case "correct":
                        model.Correct(); break;
                    case "open_categories":
                        Process.Start(BookCommonData.SETTINGS_PATH + "categories.txt"); break;
                    case "open_corrections":
                        Process.Start(BookCommonData.SETTINGS_PATH + "corrections.txt"); break;
                    case "open_hidden":
                        Process.Start(BookCommonData.SETTINGS_PATH + "hidden.txt"); break;
                    case "open_suggestions":
                        Process.Start(BookCommonData.SETTINGS_PATH + "suggestions.txt"); break;
                    case "update_isbn":
                        await model.UpdateISBN(); break;
                    case "update_cat":
                        await model.UpdateCategories(); break;
                    case "update_year":
                        await model.UpdateYear(); break;
                    case "modify_in":
                        await model.Modify_IN_ID(); break;
                    case "reset_edition":
                        model.ResetEdition(); break;
                    case "2edition":
                        await model.CheckEdition(2); break;
                    case "3edition":
                        await model.CheckEdition(3); break;
                    case "obsolete":
                        model.MakeObsolete(); break;
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
            model.GoBack();
        }

        private void ctxOpenBook(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItem != null && listView.SelectedItem is Book)
            {
                model.OpenBook(listView.SelectedItem as Book);
            }
        }

        private void ctxOpenFolder(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItem != null && listView.SelectedItem is Book && ((Book)listView.SelectedItem).IsDownloaded)
            {
                Process.Start(Path.GetDirectoryName(((Book)listView.SelectedItem).LocalPath));
            }
        }
    }
}
