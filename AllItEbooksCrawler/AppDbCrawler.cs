﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace BookUtils
{
    public delegate void NotifyHandler(string message);

    public class AppDbCrawler : IDisposable
    {
        public static int PAGES_NUM = 50;
        public static int DELAY = 50;
        public event NotifyHandler Notify;
        private AppDbContext db;

        public string LastAction { get; set; }

        public void AddBook(Book model)
        {
            db.Books.Add(model);
            db.SaveChanges();
            LastAction = "ADD";
        }

        public void RemoveBook(Book model)
        {
            db.Books.Remove(model);
            db.SaveChanges();
            LastAction = "REMOVE";
        }

        public void ClearFile()
        {
            db.SaveChanges();
            db.Dispose();
            db = null;
            File.Delete(@"..\..\books.db");
            LastAction = "CLEAR";
        }

        public int GetCustomPostId()
        {
            var min = db.Books.Min(b => b.PostId);
            return min > 0 ? -1 : min - 1;
        }

        public AppDbCrawler()
        {
            db = new AppDbContext();
        }

        public List<Book> LoadBooksFromDb()
        {
            return db.Books.ToList();
        }

        public void Correct()
        {
            var corrections = CommonData.Corrections;
            Notify("Starting correction...");
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
            LastAction = "CORRECT";
            Notify("Correction finished.");
        }

        public void Save()
        {
            db.SaveChanges();
            LastAction = "SAVE";
        }

        public async Task<int> UpdateEpubsFromWeb()
        {
            Notify("Updating epubs...");
            var web = new HtmlWeb();
            HtmlDocument detailPage = null;
            var list = db.Books.Where(b => b.Extension == "epub").ToList();
            var counter = 0;
            foreach (var book in list)
            {
                Notify($"Updating epubs {++counter}/{list.Count}...");
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
                    else
                    {
                        book.DownloadUrl = href;
                        book.Extension = "epub";
                    }
                }
            }
            db.SaveChanges();
            LastAction = "UPDATE";
            Notify("Updating completed...");
            return counter;
        }

        public async Task<Book> UpdateBookFromWeb(Book book)
        {
            Dictionary<string, string> corrections = CommonData.Corrections;
            Notify($"Updating book {book.ClearTitle} from web...");
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
                        _category += anode.Attributes["href"].Value.Replace("http://www.allitebooks.org/", "").TrimEnd('/') + ";";
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

        public async Task<int> UpdateDbFromWeb()
        {
            var corrections = CommonData.Corrections;
            try
            {
                Notify("Updating all from web...");
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

                for (var page = 1; page <= PAGES_NUM; page++)
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
                            book = await UpdateBookFromWeb(book);
                            Notify($"Loading page {page}...");
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
                    Notify($"Page {page} saved.");
                    if (alreadyThereCounter > MAX_THERE)
                        break;
                    Thread.Sleep(DELAY);
                }
                Notify($"Updating all finished. Added {booksAdded} new books.");
                LastAction = "UPDATE";
                return booksAdded;
            }
            catch (Exception e)
            {
                Notify($"Error while updating from web: {e.Message}");
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