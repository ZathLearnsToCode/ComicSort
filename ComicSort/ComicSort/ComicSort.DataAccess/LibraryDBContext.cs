using ComicSort.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.DataAccess
{
    public class LibraryDBContext : DbContext
    {
        public DbSet<ComicSortLibraries> Libraries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source = Libraries.db");
            base.OnConfiguring(optionsBuilder);
        }
    }
}
