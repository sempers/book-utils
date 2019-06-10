using System;
using System.Collections.Generic;
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
    public class ItEbook
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string Url { get; set; }
        public string DownloadUrl { get; set; }
        public string Authors { get; set; }
        public string Summary { get; set; }
        public string Category { get; set; }
        public string ISBN { get; set; }
        public int Pages { get; set; }
    }

    public static class Crawler
    {
        public static int PagesNum = 10;

        public static int Delay = 500;

        public async static void UpdateAllFromWeb()
        {
            HtmlDocument listPage = null;
            HtmlWeb web = new HtmlWeb();
            HtmlDocument detailPage = null;
            //WebClient wcl = new WebClient();
            var url = "http://www.allitebooks.org/page";

            var list = new List<ItEbook>();

            for (int page = 1; page <= PagesNum; page++)
            {
                try
                {
                    listPage = await web.LoadFromWebAsync($"{url}/{page}");
                }
                catch
                {
                    break;
                } 
                foreach (var bookElement in listPage.DocumentNode.SelectNodes("//article"))
                {
                    var newBook = new ItEbook();
                    newBook.Id = int.Parse(bookElement.Attributes["id"].Value.Substring(5));
                    newBook.Title = bookElement.SelectSingleNode("//h2[@class='entry-title']/a").InnerText;
                    newBook.Url = bookElement.SelectSingleNode("//h2[@class='entry-title']/a").Attributes["href"].Value;
                    newBook.Summary = bookElement.SelectSingleNode("//div[@class='entry-summary']/p").InnerText;
                    detailPage = await web.LoadFromWebAsync(newBook.Url);
                    var detail = detailPage.DocumentNode.SelectSingleNode("//div[@class='book-detail']/dl");
                    var dtNodes = detail.SelectNodes("dt");
                    var ddNodes = detail.SelectNodes("dd");
                    for (var i=0; i<dtNodes.Count; i++)
                    {
                        if (dtNodes[i].InnerText == "Year:")
                            newBook.Year = int.Parse(ddNodes[i].InnerText);
                        if (dtNodes[i].InnerText == "Category:")
                            newBook.Category = ddNodes[i].InnerText;
                        if (dtNodes[i].InnerText == "Pages:")
                            newBook.Pages = int.Parse(ddNodes[i].InnerText);
                        if (dtNodes[i].InnerText.Contains("Author"))
                            newBook.Authors = ddNodes[i].InnerText;
                    }


                    list.Add(newBook);
                }
                Thread.Sleep(Delay);
            }
            
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Crawler.UpdateAllFromWeb();
        }
    }
}
