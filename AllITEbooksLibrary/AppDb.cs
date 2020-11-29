using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using AllITEbooksLib.Properties;
using System.Text.RegularExpressions;

namespace BookUtils
{
    public delegate void NotifyHandler(string message);

    public class AppDb : IDisposable
    {
        public static int PAGES_NUM = 50;
        public static int DELAY = 50;

        event NotifyHandler OnNotify;
        AppDbContext db;

        public BookActions LastAction { get; set; }

        public void AddBook(Book model)
        {
            db.Books.Add(model);
            db.SaveChanges();
            LastAction = BookActions.Add;
        }

        public void RemoveBook(Book model)
        {
            db.Books.Remove(model);
            db.SaveChanges();
            LastAction = BookActions.Remove;
        }

        public void ClearDbFile()
        {
            db.SaveChanges();
            db.Dispose();
            db = null;
            File.Delete(@"..\..\books.db");
            LastAction = BookActions.Clear;
        }

        public int GetCustomPostId()
        {
            var min = db.Books.Min(b => b.PostId);
            return min > 0 ? -1 : min - 1;
        }

        public AppDb(NotifyHandler notifyHandler)
        {
            db = new AppDbContext();
            OnNotify += notifyHandler;
        }

        public List<Book> LoadBooksFromDb()
        {
            return db.Books.ToList();
        }

        public void Correct()
        {
            var corrections = BookCommonData.Corrections;
            OnNotify("Starting correction...");
            foreach (var book in db.Books)
            {
                book.Title = book.Title.Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");
                if (!string.IsNullOrEmpty(book.Category))
                {
                    var _category = book.Category;
                    foreach (var correction in corrections)
                    {
                        if (correction.Key.Trim().StartsWith("*"))
                            continue;
                        var key = correction.Key.Trim().Replace("*", "");
                        var value = correction.Value.Trim();
                        _category = _category.Replace(key, value);
                    }
                    if (_category.Contains(";"))
                        _category = _category.Split(';')[0];
                    book.SetCategory(_category); //no approving
                    book.Authors = book.Authors.Trim();
                }
                book.Summary = book.Summary?.Replace("&#8230;", "...");
            }
            db.SaveChanges();
            LastAction = BookActions.Correct;
            OnNotify("Correction finished.");
        }

        public void Save()
        {
            db.SaveChanges();
            LastAction = BookActions.Save;
        }

        public async Task<int> UpdateEpubsFromWeb()
        {
            OnNotify("Updating epubs...");
            var web = new HtmlWeb();
            HtmlDocument detailPage = null;
            var list = db.Books.Where(b => b.Extension == "epub").ToList();
            var counter = 0;
            foreach (var book in list)
            {
                OnNotify($"Updating epubs {++counter}/{list.Count}...");
                detailPage = await web.LoadFromWebAsync(book.Url);
                var downloadLinks = detailPage.DocumentNode.SelectNodes("//span[@class='download-links']/a");
                foreach (var node in downloadLinks)
                {
                    var href = node.Attributes["href"].Value;
                    if (href.Contains(".pdf"))
                    {
                        book.DownloadUrl = href;
                        book.Extension = "pdf";
                        break;
                    }
                    else if (href.Contains(".epub"))
                    {
                        book.DownloadUrl = href;
                        book.Extension = "epub";
                    }
                    else if (href.Contains("file.html"))
                    {
                        book.DownloadUrl = href;
                        book.Extension = "rar";
                    }
                }
            }
            db.SaveChanges();
            LastAction = BookActions.Update;
            OnNotify("Updating completed...");
            return counter;
        }

        public string CatchCategory(string s)
        {
            if (s == null)
                return "";
            var m = (new Regex(@"category-((\w|\-)+)\s")).Match(s);
            var cat = m.Success ? m.Groups[1].Value : "";
            var tags = (new Regex(@"tag-((\w|\-)+)\b")).Matches(s);
            for (var i = 0; i < tags.Count; i++)
            {
                var tag = tags[i].Groups[1].Value;
                if (BookCommonData.Categories.Contains($"{cat}/{tag}"))
                    return $"{cat}/{tag}";
            }
            return cat;
        }

        public async Task<Book> UpdateBookFromWeb_ORG(Book book, string addNotify = "")
        {
            Dictionary<string, string> corrections = BookCommonData.Corrections;
            book.Source = "ORG";
            OnNotify($"Updating book {book.ClearTitle} from web...");
            var web = new HtmlWeb();
            var detailPage = await web.LoadFromWebAsync(book.Url);
            var detail = detailPage.DocumentNode.SelectSingleNode("//div[@class='book-detail']/dl");
            var dtNodes = detail.SelectNodes("dt");
            var ddNodes = detail.SelectNodes("dd");
            for (var i = 0; i < dtNodes.Count; i++)
            {
                if (dtNodes[i].InnerText == "Year:")
                    book.Year = int.Parse(ddNodes[i].InnerText);
                if (dtNodes[i].InnerText == "Category:")
                {
                    var _category = "";
                    foreach (var anode in ddNodes[i].SelectNodes("a"))
                    {
                        _category += anode.Attributes["href"].Value.Replace("http://www.allitebooks.org/", "").Replace("http://www.allitebooks.com/", "").TrimEnd('/') + ";";
                    }
                    _category = _category.Substring(0, _category.Length - 1);
                    foreach (var correction in corrections)
                    {
                        var key = correction.Key.Trim().Replace("*", "");
                        _category = _category.Replace(key, correction.Value.Trim());
                    }
                    if (_category.Contains(";"))
                        _category = _category.Split(';')[0];
                    book.Category = _category;
                }

                if (dtNodes[i].InnerText == "ISBN-10:")
                    book.ISBN = ddNodes[i].InnerText;

                if (dtNodes[i].InnerText == "Pages:")
                    book.Pages = int.Parse(ddNodes[i].InnerText);

                if (dtNodes[i].InnerText.Contains("Author"))
                    book.Authors = ddNodes[i].InnerText?.Trim();
            }
            var downloadLinks = detailPage.DocumentNode.SelectNodes("//span[@class='download-links']/a");
            foreach (var node in downloadLinks)
            {
                var href = node.Attributes["href"].Value;
                if (href.Contains(".pdf"))
                {
                    book.DownloadUrl = href;
                    book.Extension = "pdf";
                    break;
                }
                else if (href.Contains(".epub"))
                {
                    book.DownloadUrl = href;
                    book.Extension = "epub";
                }
            }
            return book;
        }


        public async Task<Book> UpdateBookFromWeb_IN(Book book, string addNotify = "")
        {
            var corrections = BookCommonData.Corrections;
            try
            {
                OnNotify($"{addNotify + ": "}Updating book {book.ClearTitle} from web...");
                book.Source = "IN";
                var web = new HtmlWeb();
                var page = await web.LoadFromWebAsync(book.Url.AdjustDomain());
                var article = page.DocumentNode.SelectSingleNode("//article");
                if (article == null)
                {
                    OnNotify("Error updating: the book page does not exist.");
                    return book;
                }
                if (book.PostId == 0)
                    book.PostId = int.Parse(article.Attributes["id"].Value.Substring(5)) + 110000;
                var cat = CatchCategory(article.Attributes["class"]?.Value);
                if (string.IsNullOrEmpty(book.Category) && !string.IsNullOrEmpty(cat))
                    book.Category = cat;
                var detailText = article.SelectSingleNode("div[@class='td-post-content']").InnerText;
                var lines = detailText.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Year: "))
                    {
                        book.Year = int.Parse(line.Substring(5).Trim());
                    }
                    if (line.StartsWith("Author: "))
                    {
                        book.Authors = line.Substring(8);
                    }
                    if (line.StartsWith("ISBN-10:"))
                    {
                        book.ISBN = line.Substring(9);
                    }
                    if (line.StartsWith("Pages:"))
                    {
                        book.Pages = int.Parse(line.Substring(7).Trim());
                    }
                }
                lines = lines.Where(l => l.Trim() != "" && l.Trim() != "(adsbygoogle = window.adsbygoogle || []).push({});").ToArray();
                book.Summary = string.Join(@"\r\n", lines);

                var downloadLinks = page.DocumentNode.SelectNodes("//a[@rel='noopener']");
                foreach (var node in downloadLinks)
                {
                    var href = node.Attributes["href"].Value;
                    if (href.Contains(".pdf"))
                    {
                        book.DownloadUrl = href.Replace("%20", " ");
                        book.Extension = "pdf";
                        break;
                    }
                    else if (href.Contains(".epub"))
                    {
                        book.DownloadUrl = href.Replace("%20", " ");
                        book.Extension = "epub";
                    }
                    else if (href.Contains("file.html"))
                    {
                        book.DownloadUrl = href.Trim();
                        book.Extension = "rar";
                    }
                }
            }
            catch (Exception e)
            {
                OnNotify($"Error while updating book {book.Title}: {e.Message}");
            }
            return book;
        }

        public async Task<int> UpdateDbFromWeb_ORG()
        {
            var corrections = BookCommonData.Corrections;
            try
            {
                OnNotify("Updating all from web...");
                HtmlDocument pageHtml = null;
                var web = new HtmlWeb();
                var postIDs = db.Books.Select(b => b.PostId).Distinct();
                var URL = "http://www.allitebooks.org/page";
                pageHtml = await web.LoadFromWebAsync("http://www.allitebooks.org");
                var pagination = pageHtml.DocumentNode.SelectNodes("//div[@class='pagination clearfix']/a");
                PAGES_NUM = int.Parse(pagination.Last().InnerText);
                int MAX_THERE = 10;
                var alreadyThereCounter = 0;
                int booksAdded = 0;

                for (var page = 0; page <= PAGES_NUM; page++)
                {
                    var pageList = new List<Book>();
                    var pageErrUrls = new List<string>();
                    pageHtml = await web.LoadFromWebAsync($"{URL}/{page}");
                    var pageBookNodes = pageHtml.DocumentNode.SelectNodes("//article");
                    //цикл по нодам страницы
                    foreach (var bookElement in pageBookNodes)
                    {
                        var book = new Book();
                        try
                        {
                            book.PostId = int.Parse(bookElement.Attributes["id"].Value.Substring(5));
                            if (postIDs.Contains(book.PostId))
                            {
                                alreadyThereCounter++;
                                if (alreadyThereCounter <= MAX_THERE) // продолжаем цикл
                                {
                                    continue;
                                }
                                else
                                {
                                    break; //убрали goto
                                }
                            }
                            //Adding book
                            //Primary properties
                            book.Title = bookElement.SelectSingleNode("div/header/h2[@class='entry-title']/a")?.InnerText;
                            book.Title = book.Title?.Trim().Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");
                            book.Url = bookElement.SelectSingleNode("div/header/h2[@class='entry-title']/a")?.Attributes["href"]?.Value?.Trim();
                            book.Summary = bookElement.SelectSingleNode("div/div[@class='entry-summary']/p")?.InnerText;
                            book.Sync = 0;
                            //Secondary properties
                            book = await UpdateBookFromWeb_ORG(book);
                            OnNotify($"Loading page {page}...");
                            pageList.Add(book);
                        }
                        catch (Exception e)
                        {
                            pageErrUrls.Add(book.Url);
                        }
                    }
                    //сюда уходит goto
                    foreach (var b in pageList)
                    {
                        if (!db.Books.Any(_b => _b.PostId == b.PostId))
                        {
                            db.Books.Add(b);
                            booksAdded++;
                        }
                    }
                    await db.SaveChangesAsync();
                    File.AppendAllLines("log.txt", pageErrUrls.ToArray());
                    OnNotify($"Page {page} saved.");
                    if (alreadyThereCounter > MAX_THERE)
                        break;
                    Thread.Sleep(DELAY);
                }
                OnNotify($"Updating all finished. Added {booksAdded} new books.");
                LastAction = BookActions.Update;
                return booksAdded;
            }
            catch (Exception e)
            {
                OnNotify($"Error while updating from web: {e.Message}");
                return 0;
            }
        }

        public async Task<int> UpdateDbFromWeb_IN()
        {
            var corrections = BookCommonData.Corrections;
            try
            {
                OnNotify("Updating all from web...");
                HtmlDocument pageHtml = null;
                var web = new HtmlWeb();
                var maxPostID = db.Books.Select(b => b.PostId).Max();
                var bookTitles = db.Books.Where(b => b.PostId > 33634).Select(b => b.Title);
                pageHtml = await web.LoadFromWebAsync(Settings.Default.WebUrl);
                var pagination = pageHtml.DocumentNode.SelectNodes("//span[@class='pages']");
                var pagText = pagination.First().InnerText;
                var lastnum = pagText.Substring(pagText.LastIndexOf(' ') + 1);
                PAGES_NUM = int.Parse(lastnum);
                int MAX_THERE = 2;
                var alreadyThereCounter = 0;
                int booksAdded = 0;

                for (var page = 1; page <= PAGES_NUM; page++)
                {
                    var newBooks = new List<Book>();
                    var pageErrUrls = new List<string>();
                    pageHtml = await web.LoadFromWebAsync($"{Settings.Default.WebUrl}/page/{page}");
                    var pageBookNodes = pageHtml.DocumentNode.SelectNodes("//div[@class='meta-info-container']");  //h3[@class='entry-title td-module-title']/a");
                                                                                                                   //цикл по нодам страницы

                    foreach (var bookNode in pageBookNodes)
                    {
                        var book = new Book();
                        try
                        {
                            var h3 = bookNode.SelectSingleNode("div/div/h3/a");
                            var title = h3.Attributes["title"].Value.Trim().Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");

                            if (bookTitles.Contains(title))
                            {
                                alreadyThereCounter++;
                                if (alreadyThereCounter <= MAX_THERE) // продолжаем цикл
                                {
                                    continue;
                                }
                                else
                                {
                                    break; //убрали goto
                                }
                            }
                            //Adding book
                            //Primary properties -> goto Details
                            book.Title = title;
                            book.Url = bookNode.SelectSingleNode("div/div[@class='td-read-more']/a").Attributes["href"].Value;
                            //Secondary properties

                            book = await UpdateBookFromWeb_IN(book);
                            OnNotify($"Loading page {page}...");
                            newBooks.Add(book);
                        }
                        catch (Exception e)
                        {
                            pageErrUrls.Add(book.Url);
                        }
                    }
                    //сюда уходит goto
                    foreach (var b in newBooks)
                    {
                        if (!db.Books.Any(_b => _b.PostId == b.PostId))
                        {
                            db.Books.Add(b);
                            booksAdded++;
                        }
                    }
                    await db.SaveChangesAsync();
                    File.AppendAllLines("log.txt", pageErrUrls.ToArray());
                    OnNotify($"Page {page} saved.");
                    if (alreadyThereCounter > MAX_THERE)
                        break;
                    Thread.Sleep(DELAY);
                }
                OnNotify($"Updating all finished. Added {booksAdded} new books.");
                LastAction = BookActions.Update;
                return booksAdded;
            }
            catch (Exception e)
            {
                OnNotify($"Error while updating from web: {e.Message}");
                return 0;
            }
        }

        /**/
        public void Dispose()
        {
            db.Dispose();
        }
    }
}