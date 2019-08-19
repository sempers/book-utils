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

        public void AddBook(Book model)
        {
            db.Books.Add(model);
            db.SaveChanges();
        }

        public void ClearFile()
        {
            db.SaveChanges();
            db.Dispose();
            db = null;
            File.Delete(@"..\..\books.db");
        }

        public void SaveBook(Book model)
        {
            db.SaveChanges();
        }

        public int GetCustomPostId()
        {
            var min = db.Books.Min(b => b.PostId);
            if (min > 0)
                return -1;
            else
                return min - 1;
        }

        public AppDbCrawler()
        {
            db = new AppDbContext();
        }

        public List<Book> GetFromDb()
        {
            return db.Books.ToList();
        }

        public void Correct(Dictionary<string, string> corrections)
        {
            Notify("Starting correction...");
            foreach (var book in db.Books)
            {
                book.Title = book.Title.Replace("&#8217;", "'").Replace("&#8211;", "-").Replace("&#038;", "&").Replace("&amp;", "&");
                if (book.Category != null)
                {
                    var oldPath = book.IsDownloaded ? book.LocalPath: null;
                    foreach (var correction in corrections)
                    {
                        book.Category = book.Category.Replace(correction.Key.Trim(), correction.Value.Trim());
                    }
                    book.Category = book.FirstCategory;

                    if (oldPath != null && book.LocalPath != oldPath)
                    {
                        book.AutoMove(oldPath);
                    }
                }
                book.Summary = book.Summary.Replace("&#8230;", "...");
            }
            db.SaveChanges();
            Notify("Correction finished.");
        }

        /*public void UpdateCategory(int id, string category)
        {

            var book = db.Books.Find(id);
            if (book != null)
            {
                book.Category = category;
                book.Approved = 1;
                db.SaveChanges();
            }
        }

        public void UpdateRating(int id, int rating)
        {

            var book = db.Books.Find(id);
            if (book != null)
            {
                book.Rating = rating;
                db.SaveChanges();
            }
        }
        internal void SyncBook(int id, int value = 1)
        {
            var book = db.Books.Find(id);
            if (book != null)
            {
                book.Sync = value;
                db.SaveChanges();
            }
        }
             */

        public void Save()
        {
            db.SaveChanges();
        }

        public async Task<int> UpdateDbFromWeb(Dictionary<string, string> corrections)
        {
            Notify("Updating all from web...");
            HtmlDocument pageHtml = null;
            var web = new HtmlWeb();
            HtmlDocument detailPage = null;

            var postIDs = db.Books.Select(b => b.PostId).Distinct();

            var URL = "http://www.allitebooks.org/page";

            pageHtml = await web.LoadFromWebAsync("http://www.allitebooks.org");
            var pagination = pageHtml.DocumentNode.SelectNodes("//div[@class='pagination clearfix']/a");
            PAGES_NUM = int.Parse(pagination.Last().InnerText);
            var alreadyThereCounter = 0;
            int booksAdded = 0;
            for (var page = 1; page <= PAGES_NUM; page++)
            {
                var list = new List<Book>();
                var errUrls = new List<string>();
                pageHtml = await web.LoadFromWebAsync($"{URL}/{page}");
                var pageBookNodes = pageHtml.DocumentNode.SelectNodes("//article");

                foreach (var bookElement in pageBookNodes)
                {
                    var book = new Book();
                    try
                    {
                        book.PostId = int.Parse(bookElement.Attributes["id"].Value.Substring(5));
                        if (postIDs.Contains(book.PostId))
                        {
                            alreadyThereCounter++;
                            if (alreadyThereCounter > 10)
                            {
                                goto LABEL_FINAL;
                            }
                            else
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
                        book.Sync = 0;
                        Notify($"Loading page {page}...");
                        list.Add(book);
                    }
                    catch (Exception e)
                    {
                        errUrls.Add(book.Url);
                        var x = 1;
                    }

                    LABEL_FINAL:
                    foreach (var b in list)
                    {
                        if (!db.Books.Any(_b => _b.PostId == b.PostId))
                        {
                            db.Books.Add(b);
                            booksAdded++;
                        }
                    }
                    await db.SaveChangesAsync();
                    File.AppendAllLines("log.txt", errUrls.ToArray());
                    Thread.Sleep(DELAY);
                }

                Notify($"Page {page} saved.");
                if (alreadyThereCounter > 10)
                    break;
            }
            Notify($"Updating all finished. Added {booksAdded} new books.");
            return booksAdded;
        }

        /**/

        public void Dispose()
        {
            db.Dispose();
        }
    }
}