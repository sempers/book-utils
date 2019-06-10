using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
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
    }
    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Crawler crawler;

        MainWindowModel model = new MainWindowModel();
        
        public MainWindow()
        {
            InitializeComponent();
            crawler = new Crawler();
            crawler.Notified += Crawler_Notified;
            DataContext = model;
        }

        private void Crawler_Notified(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { model.Message = message; });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            crawler.UpdateAllFromWeb();
        }
    }
}
