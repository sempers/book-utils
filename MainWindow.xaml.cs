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
    public class MainWindowModel: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _message;
        public string Message { get { return _message; } set { _message = value; OnPropertyChanged("Message"); } }

        public HashSet<string> Sortings = new HashSet<string>();

        public ObservableCollection<Book> Books { get; set; }

        public ObservableCollection<string> Categories { get; set; }

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

        public const string ITBOOKS = @"D:\it-ebooks";
        
        public MainWindow()
        {
            InitializeComponent();
            crawler = new Crawler();
            crawler.Notify += Crawler_Notified;
            DataContext = model;
            UpdateFromDb();
            SuggestCategories();
            UpdateCategoriesDropBox();
        }

        private void UpdateFromDb()
        {
            var list = crawler.GetFromDb();
            model.Books.Clear();
            list = list.OrderBy(b => -b.Year).ToList();
            list.ForEach(b => {
                var path = CalcPath(b);
                if (File.Exists(path))
                { b.IsChecked = true; }
                b.LocalPath = path;
            });
            foreach (var book in list)
            {
                model.Books.Add(book);
            }
        }

        public void MakeDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public string BreveAuthors(string src)
        {
            var split = src.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 2)
                return $"{split[0]}, {split[1]} et al";
            else
                return src;
        }

        public void UpdateCategoriesDropBox()
        {
            List<string> list = new List<string>();
            foreach (var book in model.Books)
            {
                var category = book.Category;
                var firstCat = category.Split(';')[0];
                if (!list.Contains(firstCat))
                    list.Add(firstCat);
            }
            foreach (var add in File.ReadAllLines("categories.txt"))
            {
                if (!list.Contains(add))
                list.Add(add);
            }
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
                {"Angular", "web/frameworks/angular-js" },
                {"Javascript", "web/javascript" },
                {"Java", "programming/java" },
                {"Blockchain|Bitcoin|Ethereum", "programming/blockchain" }
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

        public string CalcPath(Book book)
        {
            try
            {
                var category = book.Category;
                var firstCat = category.Split(';')[0];

                var dirs = firstCat.Split('/');
                var middlePart = "";
                if (dirs.Length == 1)
                {
                    middlePart = Path.Combine(ITBOOKS, dirs[0]);
                    MakeDir(Path.Combine(ITBOOKS, dirs[0]));
                }
                if (dirs.Length == 2)
                {
                    middlePart = Path.Combine(ITBOOKS, dirs[0], dirs[1]);
                    MakeDir(Path.Combine(ITBOOKS, dirs[0]));
                    MakeDir(Path.Combine(ITBOOKS, dirs[0], dirs[1]));
                }
                if (dirs.Length == 3)
                {
                    middlePart = Path.Combine(ITBOOKS, dirs[0], dirs[1], dirs[2]);
                    MakeDir(Path.Combine(ITBOOKS, dirs[0]));
                    MakeDir(Path.Combine(ITBOOKS, dirs[0], dirs[1]));
                    MakeDir(Path.Combine(ITBOOKS, dirs[0], dirs[1], dirs[2]));
                }
                var filename = $"[{book.Year}] {book.Title.Trim().Replace(":", "_")} - {BreveAuthors(book.Authors.Trim().Replace(":", "_"))}.pdf";
                var path = Path.Combine(middlePart, filename);
                return path;
            }
            catch {
                return null;
            }
        }

        private void Notify(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async Task DownloadCheckedAsync()
        {
            //Application.Current.Dispatcher.Invoke(() => { model.Message = "Downloads initialized..."; });
            List<Book> subset = model.Books.Where(b => b.IsChecked && !string.IsNullOrEmpty(b.DownloadUrl) && !File.Exists(CalcPath(b))).ToList();
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
                        var path = CalcPath(book);
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
            } else
            {
                model.Sortings.Add(column);
                return 1;
            }
        }

        private void SortList(string column)
        {
            List<Book> newList = null;
            switch (column)
            {
                case "Title":
                if (GetSorting("Title") < 0)
                    newList = model.Books.OrderByDescending(b => b.Title).ToList();
                else
                    newList = model.Books.OrderBy(b => b.Title).ToList();
                    model.Books.Clear();
                    foreach (var book in newList) 
                    {
                        model.Books.Add(book);
                    }
                    break;
                case "Year":
                if (GetSorting("Year") < 0)
                    newList = model.Books.OrderByDescending(b => b.Year).ToList();
                else
                    newList = model.Books.OrderBy(b => b.Year).ToList();
                    model.Books.Clear();
                    foreach (var book in newList)
                    {
                        model.Books.Add(book);
                    }
                    break;
                case "Category":
                if (GetSorting("Category") < 0)
                    newList = model.Books.OrderByDescending(b => b.Category).ToList();
                else
                    newList = model.Books.OrderBy(b => b.Category).ToList();
                    model.Books.Clear();
                    foreach (var book in newList)
                    {
                        model.Books.Add(book);
                    }
                    break;
            }
        }        

        private void Crawler_Notified(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await crawler.UpdateAllFromWeb();
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
            crawler.CorrectTitles();
            UpdateFromDb();
            UpdateCategoriesDropBox();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Book item = listView.SelectedItem as Book;
            if (item == null)
                return;
            catListBox.SelectedValue = item.Category.Split(';')[0];
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
            if (item == null)
                return;
            if (item.Category.Split(';')[0] == catListBox.SelectedItem.ToString())
                return;
            else
            {
                item.Category = catListBox.SelectedItem.ToString();
                if (item.Approved == 0) item.Approved = 1;
                if (item.Suggested) item.Suggested = false;
                crawler.ChangeCategory(item.Id, item.Category);
                if (File.Exists(item.LocalPath))
                {
                    var newPath = CalcPath(item);
                    try
                    {
                        File.Move(item.LocalPath, newPath);
                    }
                    catch { }
                    item.LocalPath = newPath;
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
    }
}
