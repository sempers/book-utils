using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllItEbooksCrawler
{
    public class BookFilter
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public bool OnlySync { get; set; }
    }
}
