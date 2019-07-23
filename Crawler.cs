using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace AllItEbooksCrawler
{
    public delegate void NotifyHandler(string message);

    public class PostIdComparer : IEqualityComparer<Book>
    {
        public bool Equals(Book a, Book b)
        {
            if (a == null || b == null)
                return false;

            return (a.PostId == b.PostId);

        }

        public int GetHashCode(Book book)
        {
            if (ReferenceEquals(book, null)) return 0;

            return book.PostId;
        }
    }

    public class Crawler
    {
        public static int PAGES_NUM = 50;

        public static int DELAY = 50;

        public event NotifyHandler Notify;

        public List<Book> GetFromDb()
        {
            using (var db = new AppDbContext())
            {
                return db.Books.ToList();
            }
        }

        public void MakeDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void Correct(Dictionary<string, string> corrections)
        {
            Notify("Starting correction...");
            using (var db = new AppDbContext())
            {
                foreach (var book in db.Books)
                {
                    book.Title = book.Title.Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");
                    if (book.Category != null)
                        foreach (var correction in corrections)
                        {
                            book.Category = book.Category.Replace(correction.Key.Trim(), correction.Value.Trim());
                        }
                    book.Category = book.FirstCategory;
                    book.Summary = book.Summary.Replace("&#8230;", "...");
                }
                db.SaveChanges();
            }
            Notify("Correction finished.");
        }

        public void ChangeCategory(int id, string category)
        {
            using (var db = new AppDbContext())
            {
                var book = db.Books.Find(id);
                if (book != null)
                {
                    book.Category = category;
                    book.Approved = 1;
                    db.SaveChanges();
                }
            }
        }

        public async Task UpdateDbFromWeb(Dictionary<string, string> corrections)
        {
            using (var db = new AppDbContext())
            {

                Notify("Updating all from web...");
                HtmlDocument listPage = null;
                HtmlWeb web = new HtmlWeb();
                HtmlDocument detailPage = null;

                var url = "http://www.allitebooks.org/page";                

                listPage = await web.LoadFromWebAsync("http://www.allitebooks.org");
                var pagination = listPage.DocumentNode.SelectNodes("//div[@class='pagination clearfix']/a");
                PAGES_NUM = int.Parse(pagination.Last().InnerText);
                var alreadyThereCounter = 0;
                for (var page = 1; page <= PAGES_NUM; page++)
                {
                    var list = new List<Book>();
                    var errUrls = new List<string>();
                    listPage = await web.LoadFromWebAsync($"{url}/{page}");
                    var pageBookNodes = listPage.DocumentNode.SelectNodes("//article");
                    
                    foreach (var bookElement in pageBookNodes)
                    {
                        var book = new Book();
                        try
                        {
                            book.PostId = int.Parse(bookElement.Attributes["id"].Value.Substring(5));
                            if (db.Books.Any(b => b.PostId == book.PostId))
                            {
                                alreadyThereCounter++;
                                if (alreadyThereCounter > 10)
                                {
                                    goto final;
                                } else
                                {
                                    continue;
                                }
                            }
                            
                            book.Title = bookElement.SelectSingleNode("div/header/h2[@class='entry-title']/a").InnerText;
                            book.Title = book.Title.Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");
                            book.Url = bookElement.SelectSingleNode("div/header/h2[@class='entry-title']/a").Attributes["href"].Value;
                            book.Summary = bookElement.SelectSingleNode("div/div[@class='entry-summary']/p").InnerText;
                            detailPage = await web.LoadFromWebAsync(book.Url);
                            var detail = detailPage.DocumentNode.SelectSingleNode("//div[@class='book-detail']/dl");
                            var dtNodes = detail.SelectNodes("dt");
                            var ddNodes = detail.SelectNodes("dd");
                            for (var i = 0; i < dtNodes.Count; i++)
                            {
                                if (dtNodes[i].InnerText == "Year:")
                                    book.Year = int.Parse(ddNodes[i].InnerText);
                                if (dtNodes[i].InnerText == "Category:")
                                {
                                    var s = "";
                                    foreach (var anode in ddNodes[i].SelectNodes("a"))
                                    {
                                        s += anode.Attributes["href"].Value.Replace("http://www.allitebooks.org/", "").TrimEnd('/') + ";";
                                    }
                                    s = s.Substring(0, s.Length - 1);
                                    book.Category = s;
                                    
                                    foreach (var correction in corrections)
                                    {
                                        book.Category = book.Category.Replace(correction.Key.Trim(), correction.Value.Trim());
                                    }
                                    book.Category = book.FirstCategory;
                                }
                                    
                                if (dtNodes[i].InnerText == "Pages:")
                                    book.Pages = int.Parse(ddNodes[i].InnerText);
                                if (dtNodes[i].InnerText.Contains("Author"))
                                    book.Authors = ddNodes[i].InnerText;
                            }
                            var downloadLinks = detailPage.DocumentNode.SelectNodes("//span[@class='download-links']/a");
                            foreach (var node in downloadLinks)
                            {
                                var href = node.Attributes["href"].Value;
                                if (href.Contains(".pdf"))
                                    book.DownloadUrl = href;
                            }
                            Notify($"Loading page {page}...");
                            list.Add(book);
                        }
                        catch (Exception e)
                        {
                            errUrls.Add(book.Url);
                            var x = 1;
                        }
                        final:
                        foreach (var b in list)
                        {
                            if (!db.Books.Any(_b => _b.PostId == b.PostId))
                                db.Books.Add(b);
                        }
                        await db.SaveChangesAsync();
                        File.AppendAllLines("log.txt", errUrls.ToArray());
                        Thread.Sleep(DELAY);
                    }
                   
                    Notify($"Page {page} saved.");
                    if (alreadyThereCounter > 10)
                        break;
                }
                Notify("Updating all finished.");
            }
        }
    }
}