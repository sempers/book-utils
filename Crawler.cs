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

        public static string ITBOOKS = @"D:\it-ebooks";

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

        public void DownloadBook(Book book)
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
                var filename = $"[{book.Year}] {book.Title.Trim().Replace(":", "_")} - {book.Authors.Trim().Replace(":", "_")}.pdf";
                var path = Path.Combine(middlePart, filename);
                if (File.Exists(path))
                    File.Delete(path);
                File.WriteAllText(path, "");
            }
            catch { }
        }

        public void DownloadAll()
        {
            Notify("Starting download...");
            using (var db = new AppDbContext())
            {
                foreach (var book in db.Books)
                {
                    if (!string.IsNullOrEmpty(book.DownloadUrl))
                    DownloadBook(book);
                }
            }
            Notify("Download finished");
        }

        public void CorrectTitles()
        {
            Notify("Starting correction...");
            using (var db = new AppDbContext())
            {
                foreach (var book in db.Books)
                {
                    book.Title = book.Title.Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");
                    book.Category = book.Category.Replace("datebases", "databases")
                        //.Replace("computers-technology/ai-machine-learning", "ai-machine-learning")
                        //.Replace("web-development", "web")
                        //.Replace("computers-technology/computer-science", "computer-science")
                        //.Replace("front-end-frameworks", "frameworks")
                        //.Replace("programming/c;programming/net", "c-sharp")
                        .Replace("angularjs", "angular-js")
                        ;
                }
                db.SaveChanges();
            }
            Notify("Correction finished.");
        }

        public void SuggestCategories()
        {
           
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

        public async Task UpdateAllFromWeb()
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

                for (var page = 1; page <= PAGES_NUM; page++)
                {
                    var list = new List<Book>();
                    List<string> errUrls = new List<string>();
                    listPage = await web.LoadFromWebAsync($"{url}/{page}");
                    var pageBookNodes = listPage.DocumentNode.SelectNodes("//article");
                    foreach (var bookElement in pageBookNodes)
                    {
                        var newBook = new Book();
                        try
                        {
                            newBook.PostId = int.Parse(bookElement.Attributes["id"].Value.Substring(5));
                            newBook.Title = bookElement.SelectSingleNode("div/header/h2[@class='entry-title']/a").InnerText;
                            newBook.Url = bookElement.SelectSingleNode("div/header/h2[@class='entry-title']/a").Attributes["href"].Value;
                            newBook.Summary = bookElement.SelectSingleNode("div/div[@class='entry-summary']/p").InnerText;
                            detailPage = await web.LoadFromWebAsync(newBook.Url);
                            var detail = detailPage.DocumentNode.SelectSingleNode("//div[@class='book-detail']/dl");
                            var dtNodes = detail.SelectNodes("dt");
                            var ddNodes = detail.SelectNodes("dd");
                            for (var i = 0; i < dtNodes.Count; i++)
                            {
                                if (dtNodes[i].InnerText == "Year:")
                                    newBook.Year = int.Parse(ddNodes[i].InnerText);
                                if (dtNodes[i].InnerText == "Category:")
                                {
                                    var s = "";
                                    foreach (var anode in ddNodes[i].SelectNodes("a"))
                                    {
                                        s += anode.Attributes["href"].Value.Replace("http://www.allitebooks.org/", "").TrimEnd('/') + ";";
                                    }
                                    s = s.Substring(0, s.Length - 1);
                                    newBook.Category = s;
                                }
                                    
                                if (dtNodes[i].InnerText == "Pages:")
                                    newBook.Pages = int.Parse(ddNodes[i].InnerText);
                                if (dtNodes[i].InnerText.Contains("Author"))
                                    newBook.Authors = ddNodes[i].InnerText;
                            }
                            var downloadLinks = detailPage.DocumentNode.SelectNodes("//span[@class='download-links']/a");
                            foreach (var node in downloadLinks)
                            {
                                var href = node.Attributes["href"].Value;
                                if (href.Contains(".pdf"))
                                    newBook.DownloadUrl = href;
                            }
                            Notify($"Loading page {page}...");
                            list.Add(newBook);
                        }
                        catch (Exception e)
                        {
                            errUrls.Add(newBook.Url);
                            var x = 1;
                        }
                        foreach (var book in list)
                        {
                            if (!db.Books.Any(b => b.PostId == book.PostId))
                                db.Books.Add(book);
                        }
                        await db.SaveChangesAsync();
                        File.AppendAllLines("log.txt", errUrls.ToArray());
                        Thread.Sleep(DELAY);
                    }
                   
                    
                    Notify($"Page {page} saved.");
                }
                Notify("Updating all finished.");
            }
        }
    }
}