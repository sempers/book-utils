using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AllItEbooksCrawler
{
    public delegate void NotifyHandler(string message);

    public class Crawler
    {
        public static int PAGES_NUM = 1;

        public static int DELAY = 500;

        public event NotifyHandler Notified;

        public List<Book> GetFromDb()
        {
            using (var db = new AppDbContext())
            {
                return db.Books.ToList();
            }
        }

        public async void UpdateAllFromWeb()
        {
            using (var db = new AppDbContext())
            {
                HtmlDocument listPage = null;
                HtmlWeb web = new HtmlWeb();
                HtmlDocument detailPage = null;

                var url = "http://www.allitebooks.org/page";

                var list = new List<Book>();

                for (var page = 1; page <= PAGES_NUM; page++)
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
                        var newBook = new Book();
                        newBook.Id = int.Parse(bookElement.Attributes["id"].Value.Substring(5));
                        newBook.Title = bookElement.SelectSingleNode("//h2[@class='entry-title']/a").InnerText;
                        newBook.Url = bookElement.SelectSingleNode("//h2[@class='entry-title']/a").Attributes["href"].Value;
                        newBook.Summary = bookElement.SelectSingleNode("//div[@class='entry-summary']/p").InnerText;
                        detailPage = await web.LoadFromWebAsync(newBook.Url);
                        var detail = detailPage.DocumentNode.SelectSingleNode("//div[@class='book-detail']/dl");
                        var dtNodes = detail.SelectNodes("dt");
                        var ddNodes = detail.SelectNodes("dd");
                        for (var i = 0; i < dtNodes.Count; i++)
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
                        Notified($"Loading page {page}...");
                        list.Add(newBook);
                    }
                    
                    Thread.Sleep(DELAY);
                }

                foreach (var book in list)
                {
                    db.Books.Add(book);
                }
                await db.SaveChangesAsync();
            }
        }
    }
}
