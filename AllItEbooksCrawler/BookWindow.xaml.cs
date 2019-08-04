using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BookUtils
{
    /// <summary>
    /// Interaction logic for BookWindow.xaml
    /// </summary>
    public partial class BookWindow : Window
    {
        Crawler _crawler;
        Book model;
        string _action;

        public BookWindow(Book modelBook, Crawler crawler, string action = "NEW")
        {
            InitializeComponent();
            model = modelBook;
            DataContext = model;
            _crawler = crawler;
            _action = action;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            switch (_action)
            {
                case "NEW": _crawler.AddBook(model); break;
                case "EDIT": _crawler.SaveBook(model); break;
            }
            DialogResult = true;
            Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
