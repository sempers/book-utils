using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        AppDb db;
        Book model;
        BookActions action;
        string oldCategory;

        public BookWindow(Book model, MainWindow main, BookActions action = BookActions.Add)
        {
            InitializeComponent();
            this.Owner = main;
            this.model = model;
            DataContext = this.model;
            db = main.Model.Db;
            this.action = action;
            oldCategory = model.Category;
            if (this.action == BookActions.Add)
            {
                btnRemove.Visibility = Visibility.Hidden;
            }
        }


        private void _btnOK_Click(object sender, RoutedEventArgs e)
        {
            switch (action)
            {
                case BookActions.Add: db.AddBook(model); break;
                case BookActions.Edit: db.Save(); break;
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
                db.RemoveBook(model);
                DialogResult = true; //special
                Close();
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (model.Source == "IN")
                model = await db.UpdateBookFromWeb_IN(model);
            else
                model = await db.UpdateBookFromWeb_ORG(model);
            db.Save();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {Filter="PDF, EPUB|*.pdf;*.epub",
            Multiselect=false,
            InitialDirectory=System.IO.Path.GetDirectoryName(model.DownloadUrl)
            };
            if (openFileDialog.ShowDialog() == true)
            model.DownloadUrl = openFileDialog.FileName;
        }

        private void btnGoto_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(model.Url);
        }

        private void catListBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            
        }

        private void btnRead_Click(object sender, RoutedEventArgs e)
        {
            if (model.IsDownloaded)
                Process.Start(model.LocalPath);
        }
    }
}
