using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BookUtils
{
    public class AppDbContext: DbContext
    {
        public DbSet<Book> Books { get; set; }

        public AppDbContext()
        {
            Database.SetInitializer<AppDbContext>(null);
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>().ToTable("Books");
            base.OnModelCreating(modelBuilder);            
        }
    }

    
}
