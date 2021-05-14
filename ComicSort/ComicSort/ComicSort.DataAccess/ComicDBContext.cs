using ComicSort.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.DataAccess
{
    public class ComicDBContext : DbContext
    {
        public DbSet<ComicBookList> ComicBookLists { get; set; }
        public DbSet<ComicBook> ComicBooks { get; set; }
        public DbSet<Page> Pages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source = ComicSortDB.DB");
            base.OnConfiguring(optionsBuilder);
        }

        
    }
}
