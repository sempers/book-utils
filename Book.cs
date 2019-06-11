using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllItEbooksCrawler
{
    public class Book
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string Url { get; set; }
        public string DownloadUrl { get; set; }
        public string Authors { get; set; }
        public string Summary { get; set; }
        public string Category { get; set; }
        public string ISBN { get; set; }
        public int Pages { get; set; }

        public bool IsChecked { get; set; }
        public override string ToString()
        {
            return $"[{Year}] {Authors}. {Title}";
        }
    }
}
