using ComicSort.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.DataAccess
{
    public class ComicSortLibrariesDBContext : DbContext
    {
        public DbSet<ComicSortLibraries> ComicSortLibraries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(@"Data Source = E:\Test\ComicSortdb.db");
            base.OnConfiguring(optionsBuilder);
        }

    }
}
