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

namespace AllItEbooksCrawler
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

        public const string ROOT = @"D:\it-ebooks";

        public bool Suggested { get; set; } = false;   

        public Dictionary<string, string> TxtCorrections { get; set; }
        public List<string> TxtHidden { get; set; }

        
        public MainWindow()
        {
            InitializeComponent();
            Book.ROOT = ROOT;
            crawler = new Crawler();
            crawler.Notify += Crawler_Notified;
            DataContext = model;
            LoadBooksFromDb();
            ListCategories();
        }

        public void LoadCorrections()
        {
            if (File.Exists("./settings/corrections.txt"))
            {
                TxtCorrections = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines("./settings/corrections.txt"))
                {
                    var split = line.Split('=');
                    TxtCorrections.Add(split[0], split[1]);
                }
            }
        }

        public void LoadHidden()
        {
            if (File.Exists("./settings/hidden.txt"))
            {
                TxtHidden = File.ReadAllLines("./settings/hidden.txt").ToList();
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
            int count = list.RemoveAll(b => HiddenIncludes(b));
            list.ForEach(b =>
            {
                if (File.Exists(b.LocalPath))
                {
                    b.IsChecked = true;
                }
            });
            model.Books = list;
            model.LoadList(list);
            model.Sortings.Add("PostId");
            model.SortList("PostId");
        }

        public void MakeDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void ListCategories()
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            foreach (var book in model.ShownBooks)
            {
                var firstCat = book.FirstCategory;
                if (!string.IsNullOrEmpty(firstCat))
                    if (!dict.ContainsKey(firstCat))
                        dict.Add(firstCat, 1);
                    else
                        dict[firstCat]++;
            }
            foreach (var add in File.ReadAllLines("./settings/categories.txt"))
            {
                if (!dict.ContainsKey(add))
                    dict.Add(add, 0);
            }
            List<string> list = new List<string>();
            foreach (var kv in dict)
            {
                list.Add($"{kv.Key}");
            }
            list.Add("(no category)");
            list.Sort();
            model.Categories.Clear();
            foreach (var cat in list)
            {
                model.Categories.Add(cat);
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
        }

        public void SuggestCategories()
        {
            if (File.Exists("./settings/suggestions.txt")) {
                var dict = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines("./settings/suggestions.txt"))
                {
                    var split = line.Split('=');
                    dict.Add(split[0], split[1]);
                }
                foreach (var book in model.ShownBooks.Where(b => b.Approved == 0))
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
            }
        }

        private void Notify(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private async Task DownloadCheckedAsync()
        {
            var subset = model.ShownBooks.Where(b => b.IsChecked && !string.IsNullOrEmpty(b.DownloadUrl) && !File.Exists(b.LocalPath)).ToList();
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
            await crawler.UpdateDbFromWeb(TxtCorrections);
            LoadBooksFromDb();
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
            LoadCorrections();
            crawler.Correct(TxtCorrections);
            DeleteEmptyFolders();
            LoadBooksFromDb();
            ListCategories();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Book item = listView.SelectedItem as Book;
            if (item != null)
            {
                catListBox.SelectedValue = item.FirstCategory;
                model.FilterMode = false;
            } else
            {
                model.FilterMode = true;
            }
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
            model.SortList(column);
        }

        private void catListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var items = listView.SelectedItems;
            // Фильтр по категории
            if (items.Count == 0 || model.FilterMode)
            {
                if (catListBox.SelectedItem != null)
                    model.FilterListByCategory(catListBox.SelectedItem.ToString());
                return;
            }
            //Присвоение категории
            foreach (Book item in items)
            {
                if (catListBox.SelectedItem == null || item.FirstCategory == catListBox.SelectedItem.ToString())
                    continue;
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

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(catListBox.Text) && !model.Categories.Contains(catListBox.Text))
            {
                model.Categories.Add(catListBox.Text);
                var sorted = model.Categories.OrderBy(x => x).ToList();
                model.Categories.Clear();
                foreach (var s in sorted)
                {
                    model.Categories.Add(s);
                }
            }
        }

        private void catListBox_TextInput(object sender, TextCompositionEventArgs e)
        {

        }
    }
}
