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
        AppDbCrawler _crawler;
        Book model;
        string _action;

        public BookWindow(Book modelBook, AppDbCrawler crawler, string action = "NEW")
        {
            InitializeComponent();
            model = modelBook;
            DataContext = model;
            _crawler = crawler;
            _action = action;
            if (_action == "NEW")
            {
                btnRemove.Visibility = Visibility.Hidden;
            }
        }

        private void _btnOK_Click(object sender, RoutedEventArgs e)
        {
            switch (_action)
            {
                case "NEW": _crawler.AddBook(model); break;
                case "EDIT": _crawler.Save(); break;
            }
            DialogResult = true;
            Close();
        }

        private void _btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void _btnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you really want to delete the book?", "Delete book", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _crawler.RemoveBook(model);
                DialogResult = true; //special
                Close();
            }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            _crawler.UpdateBookFromWeb(model);
        }
    }
}
