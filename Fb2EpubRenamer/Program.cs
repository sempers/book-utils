﻿using BookUtils;
using EpubSharp;
using FB2Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fb2EpubRenamer
{
    class Program
    {
        static Book2 LoadFb2(string path)
        {
            try
            {
                FB2File fb2 = (new FB2Reader()).ReadFromFile(path);
                var book = new Book2
                {
                    Authors = string.Join(", ", fb2.TitleInfo.BookAuthors.Select(s => s.FirstName.Text + " " + s.LastName.Text).ToArray()),
                    Title = fb2.TitleInfo.BookTitle.Text,
                    Path = path
                };
                return book;
            }
            catch
            {
                return null;
            }
        }

        static Book2 LoadEpub(string path)
        {
            try
            {
                EpubBook epub = EpubReader.Read(path);
                var book = new Book2
                {
                    Authors = string.Join(", ", epub.Authors.ToArray()),
                    Title = epub.Title,
                    Path = path
                };
                return book;
            }
            catch
            {
                return null;
            }
        }

        static void Main(string[] args)
        {
            var root = Directory.Exists(@"D:\books\google_drive\books") ? @"D:\books\google_drive\books": ".";
            var properFilePath = Path.Combine(root, "MoonReader", "proper.txt");
            var properPaths = CommonUtils.Utils.ReadLines(properFilePath);
            if (args.Length > 0 && Directory.Exists(args[0]))
            {
                root = args[0];
            }
            Console.WriteLine($"Loading files in {root} folder...");
            var paths = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Where(path => path.EndsWith(".fb2") || path.EndsWith(".epub")).ToList();
            List<Book2> books = new List<Book2>();
            foreach (var path in paths)
            {
                //пропускаем
                if (properPaths.Contains(path))
                    continue;
                Book2 book = path.EndsWith(".fb2") ? LoadFb2(path) : LoadEpub(path);
                if (book != null)
                    books.Add(book);
            }
            Console.WriteLine($"{books.Count} files loaded");
            var log = new List<string>();
            var counter = 0;
            foreach (var book in books)
            {
                if (book.Path == book.ProperPath && !properPaths.Contains(book.ProperPath))
                {
                    properPaths.Add(book.ProperPath);
                }
                if (book.Path != book.ProperPath && !File.Exists(book.ProperPath))
                {
                    try
                    {
                        File.Move(book.Path, book.ProperPath);
                        counter++;
                        Console.WriteLine($"File '{Path.GetFileName(book.Path)} renamed to '{book.ProperFileName}.");
                        log.Add($"File '{Path.GetFileName(book.Path)} renamed to '{book.ProperFileName}.");
                        properPaths.Add(book.ProperPath);
                    } catch (Exception e)
                    {
                        log.Add($"ERROR: '{Path.GetFileName(book.Path)} could not rename to '{book.ProperFileName}: {e.Message}");
                        continue;
                    }
                }
            }
            File.WriteAllLines("log.txt", log);
            File.WriteAllLines(properFilePath, properPaths);
            Console.WriteLine($"{counter} books successfully renamed. OK!");
            Console.ReadLine();
        }
    }
}
