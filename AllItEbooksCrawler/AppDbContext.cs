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
            modelBuilder.Entity<Book>().HasKey(x => x.Id).Property(x => x.Id).HasColumnName("Id").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Book>().Ignore(x => x.IsChecked);
            modelBuilder.Entity<Book>().Property(x => x.PostId).HasColumnName("PostId");
            modelBuilder.Entity<Book>().Property(x => x.Authors).HasColumnName("Authors");
            modelBuilder.Entity<Book>().Property(x => x.Title).HasColumnName("Title");
            modelBuilder.Entity<Book>().Property(x => x.Year).HasColumnName("Year");
            modelBuilder.Entity<Book>().Property(x => x.Pages).HasColumnName("Pages");
            modelBuilder.Entity<Book>().Property(x => x.DownloadUrl).HasColumnName("DownloadUrl");
            modelBuilder.Entity<Book>().Property(x => x.Url).HasColumnName("Url");
            modelBuilder.Entity<Book>().Property(x => x.Summary).HasColumnName("Summary");
            modelBuilder.Entity<Book>().Property(x => x.Category).HasColumnName("Category");
            modelBuilder.Entity<Book>().Property(x => x.ISBN).HasColumnName("ISBN");
            modelBuilder.Entity<Book>().Property(x => x.Approved).HasColumnName("Approved");
            modelBuilder.Entity<Book>().Property(x => x.Sync).HasColumnName("Sync");
            modelBuilder.Entity<Book>().Property(x => x.Rating).HasColumnName("Rating");
            modelBuilder.Entity<Book>().Ignore(x => x.Suggested);
            modelBuilder.Entity<Book>().Ignore(x => x.OldCategory);
            base.OnModelCreating(modelBuilder);            
        }
    }

    
}
