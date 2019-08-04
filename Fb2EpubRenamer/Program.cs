using BookUtils;
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
            if (args.Length > 0 && Directory.Exists(args[0]))
            {
                root = args[0];
            }
            Console.WriteLine($"Loading files in {root} folder...");
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Where(path => path.EndsWith(".fb2") || path.EndsWith(".epub")).ToList();
            List<Book2> books = new List<Book2>();
            foreach (var file in files)
            {
                Book2 book = file.EndsWith(".fb2") ? LoadFb2(file) : LoadEpub(file);
                if (book != null)
                    books.Add(book);
            }
            Console.WriteLine($"{books.Count} files loaded");
            var log = new List<string>();
            var counter = 0;
            foreach (var book in books)
            {
                if (book.Path != book.ProperPath && !File.Exists(book.ProperPath))
                {
                    try
                    {
                        File.Move(book.Path, book.ProperPath);
                        counter++;
                        log.Add($"File '{Path.GetFileName(book.Path)} renamed to '{book.ProperFileName}.");
                    } catch (Exception e)
                    {
                        log.Add($"ERROR: '{Path.GetFileName(book.Path)} could not rename to '{book.ProperFileName}: {e.Message}");
                        continue;
                    }
                }
            }
            File.WriteAllLines("log.txt", log);
            Console.WriteLine($"{counter} books successfully renamed. OK!");
            Console.ReadLine();
        }
    }
}
