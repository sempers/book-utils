using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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

namespace AllItEbooksCrawler
{
    public class MainWindowModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _message;
        public string Message { get { return _message; } set { _message = value; OnPropertyChanged("Message"); } }

        private string _searchTitle;
        public string SearchTitle { get { return _searchTitle; } set { _searchTitle = value; OnPropertyChanged("SearchTitle"); } }

        public HashSet<string> Sortings = new HashSet<string>();

        public ObservableCollection<Book> Books { get; set; }

        public ObservableCollection<string> Categories { get; set; }

        public List<Book> UnfilteredList;

        public MainWindowModel()
        {
            Books = new ObservableCollection<Book>();
            Categories = new ObservableCollection<string>();
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Crawler crawler;

        MainWindowModel model = new MainWindowModel();

        public const string ROOT = @"D:\it-ebooks";

        public Dictionary<string, string> corrections;

        public MainWindow()
        {
            InitializeComponent();
            Book.ROOT = ROOT;
            LoadCorrections();
            crawler = new Crawler();
            crawler.Notify += Crawler_Notified;
            DataContext = model;
            UpdateFromDb();
            //SuggestCategories();
            UpdateCategoriesDropBox();
        }

        public void LoadCorrections()
        {
            if (File.Exists("./settings/corrections.txt"))
            {
                corrections = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines("./settings/corrections.txt"))
                {
                    var split = line.Split('=');
                    corrections.Add(split[0], split[1]);
                }
            }
        }

        private void UpdateFromDb()
        {
            var list = crawler.GetFromDb();
            list.ForEach(b =>
            {
                if (File.Exists(b.LocalPath))
                {
                    b.IsChecked = true;
                }
            });
            LoadList(list);
            model.Sortings.Add("PostId");
            SortList("PostId");
        }

        public void MakeDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void UpdateCategoriesDropBox()
        {
            List<string> list = new List<string>();
            foreach (var book in model.Books)
            {
                var firstCat = book.FirstCategory;
                if (!string.IsNullOrEmpty(firstCat) && !list.Contains(firstCat))
                    list.Add(firstCat);
            }
            foreach (var add in File.ReadAllLines("./settings/categories.txt"))
            {
                if (!list.Contains(add))
                    list.Add(add);
            }
            list.Add("(no category)");
            list.Sort();
            model.Categories.Clear();
            foreach (var cat in list)
            {
                model.Categories.Add(cat);
            }
        }

        public void SuggestCategories()
        {
            var DICT = new Dictionary<string, string>
            {
                {"Node.JS", "web/javascript/node-js" },
                {"R", "programming/r" },
                {"Go", "programming/go" },
                {"MongoDB", "databases/mongodb" },
                {"Android", "programming/android" },
                {"iOS", "programming/ios" },
                {"Angular", "web/frameworks/angular-js" },
                {"Javascript", "web/javascript" },
                {"Java", "programming/java" },
                {"Blockchain|Bitcoin|Ethereum", "programming/blockchain" },
                {"C++", "programming/cpp" },
                {"Amazon", "networking/cloud-computing/amazon" },
                {"Azure", "networking/cloud-computing/azure" },
                {"Unity", "game-programming/unity" }
            };
            foreach (var book in model.Books.Where(b => b.Approved == 0))
            {
                var titleWords = book.Title.Replace(",", "").Split(' ').ToList();
                foreach (var kvPair in DICT)
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
        }

        private void Notify(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async Task DownloadCheckedAsync()
        {
            List<Book> subset = model.Books.Where(b => b.IsChecked && !string.IsNullOrEmpty(b.DownloadUrl) && !File.Exists(b.LocalPath)).ToList();
            if (subset.Count == 0)
                return;
            Notify("Downloads initialized...");

            int c = 0; int total = subset.Count;
            foreach (var book in subset)
            {
                c++;
                Notify($"Downloading book {c}/{total}");
                using (var wc = new WebClient())
                {
                    var path = book.LocalPath;
                    MakeDir(Path.GetDirectoryName(path));
                    if (path != null && !File.Exists(path))
                        await wc.DownloadFileTaskAsync(new Uri(book.DownloadUrl), path);
                }
            }
            Application.Current.Dispatcher.Invoke(() => { model.Message = "Downloads finished."; });
        }

        private int GetSorting(string column)
        {
            if (model.Sortings.Contains(column))
            {
                model.Sortings.Remove(column);
                return -1;
            }
            else
            {
                model.Sortings.Add(column);
                return 1;
            }
        }

        private void FilterListByCategory(string category)
        {
            if (category == "(no category)" || string.IsNullOrEmpty(category))
            {
                if (model.UnfilteredList != null)
                {
                    LoadList(model.UnfilteredList);
                    model.UnfilteredList = null;
                }
                else
                {
                    LoadList(new List<Book>());
                }
            }
            else
            {
                if (model.UnfilteredList == null)
                    model.UnfilteredList = model.Books.ToList();
                var newList = model.UnfilteredList.FindAll(book => book.Category == category).ToList();
                LoadList(newList);
            }
        }

        private void FilterListByTitle(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                if (model.UnfilteredList != null)
                {
                    LoadList(model.UnfilteredList);
                    model.UnfilteredList = null;
                }
                else
                {
                    LoadList(new List<Book>());
                }
            }
            else
            {
                if (model.UnfilteredList == null)
                    model.UnfilteredList = model.Books.ToList();
                var newList = model.UnfilteredList.FindAll(book => book.Title.ToUpper().Contains(search.ToUpper())).ToList();
                LoadList(newList);
            }
        }

        private void LoadList(List<Book> list)
        {
            model.Books.Clear();
            foreach (var book in list)
            {
                model.Books.Add(book);
            }
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

        private void SortList(string column)
        {
            List<Book> newList = null;
            switch (column)
            {
                case "PostId":
                if (GetSorting("PostId") < 0)
                    newList = model.Books.OrderByDescending(b => b.PostId).ToList();
                else
                    newList = model.Books.OrderBy(b => b.PostId).ToList();
                break;
                case "Title":
                if (GetSorting("Title") < 0)
                    newList = model.Books.OrderByDescending(b => b.Title).ToList();
                else
                    newList = model.Books.OrderBy(b => b.Title).ToList();
                break;
                case "Year":
                if (GetSorting("Year") < 0)
                    newList = model.Books.OrderByDescending(b => b.Year).ToList();
                else
                    newList = model.Books.OrderBy(b => b.Year).ToList();
                break;
                case "Category":
                if (GetSorting("Category") < 0)
                    newList = model.Books.OrderByDescending(b => b.Category).ToList();
                else
                    newList = model.Books.OrderBy(b => b.Category).ToList();
                break;
            }
            if (newList != null)
                LoadList(newList);
        }

        private void Crawler_Notified(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await crawler.UpdateAllFromWeb(corrections);
            UpdateFromDb();
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = ((FrameworkElement)e.OriginalSource).DataContext as Book;
            Process.Start(item.Url);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DownloadCheckedAsync();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            crawler.Correct(corrections);
            DeleteEmptyFolders();
            UpdateFromDb();
            UpdateCategoriesDropBox();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Book item = listView.SelectedItem as Book;
            if (item != null)
                catListBox.SelectedValue = item.FirstCategory;
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            var x = 1;
        }

        private void ListView_Click_1(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource as GridViewColumnHeader == null)
                return;
            var column = (e.OriginalSource as GridViewColumnHeader).Content.ToString();
            SortList(column);
        }

        private void catListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = listView.SelectedItem as Book;
            // Фильтр по категории
            if (item == null)
            {
                FilterListByCategory(catListBox.SelectedItem.ToString());
                return;
            }
            //Присвоение категории
            if (item.FirstCategory == catListBox.SelectedItem.ToString())
                return;
            else
            {
                item.Category = catListBox.SelectedItem.ToString();
                if (item.Approved == 0) item.Approved = 1;
                if (item.Suggested) item.Suggested = false;
                var oldPath = item.LocalPath;
                crawler.ChangeCategory(item.Id, item.Category);
                if (File.Exists(oldPath))
                {
                    var newPath = item.LocalPath;
                    MakeDir(Path.GetDirectoryName(newPath));
                    try
                    {
                        File.Move(oldPath, newPath);
                    }
                    catch { }
                }
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space && e.Key != Key.Return && e.Key != Key.Back)
                return;
            if (e.Key == Key.Space)
            {
                var item = listView.SelectedItem as Book;
                if (item == null)
                    return;
                item.IsChecked = !item.IsChecked;
            }
            if (e.Key == Key.Return)
            {
                var item = listView.SelectedItem as Book;
                if (item == null)
                    return;
                item.Suggested = false; item.Approved = 1;
                crawler.ChangeCategory(item.Id, item.Category);
            }
            if (e.Key == Key.Back)
            {
                var item = listView.SelectedItem as Book;
                if (item == null)
                    return;
                item.Suggested = false; item.Approved = 1; item.Category = item.OldCategory;
                crawler.ChangeCategory(item.Id, item.Category);
            }
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            var search = ((TextBox)e.Source).Text;
            if (!string.IsNullOrEmpty(search))
            {
                if (search.Length >= 3)
                    FilterListByTitle(search);
            }
            else
            {
                FilterListByTitle("");
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            listView.SelectedIndex = -1;
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            SuggestCategories();
        }
    }
}
