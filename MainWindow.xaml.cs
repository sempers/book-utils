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

        public ObservableCollection<Book> Books { get; set; }

        public MainWindowModel()
        {
            Books = new ObservableCollection<Book>();
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
        }

        private void UpdateFromDb()
        {
            var list = crawler.GetFromDb();
            model.Books.Clear();
            list = list.OrderBy(b => -b.Year).ToList();
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

        private void DownloadChecked()
        {
            foreach (var book in model.Books)
            {
                if (book.IsChecked && !string.IsNullOrEmpty(book.DownloadUrl))
                {
                    using (var wc = new WebClient())
                    {
                        var path = CalcPath(book);
                        if (path != null && !File.Exists(path))
                            wc.DownloadFileAsync(new Uri(book.DownloadUrl), path);
                    }
                }
            }
            Application.Current.Dispatcher.Invoke(() => { model.Message = "Downloads initialized."; });
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
            DownloadChecked();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            crawler.CorrectTitles();
            UpdateFromDb();
        }
    }
}
