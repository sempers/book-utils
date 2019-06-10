using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AllItEbooksCrawler
{
    public class AppDbContext: DbContext
    {
        public DbSet<Book> Books { get; set; }
        
    }
}
