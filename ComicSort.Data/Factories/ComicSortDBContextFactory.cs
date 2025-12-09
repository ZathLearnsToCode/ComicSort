using ComicSort.Data.SQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Data.Factories
{
    public class ComicSortDBContextFactory : IDesignTimeDbContextFactory<ComicSortDBSQLiteContext>
    {
        public ComicSortDBSQLiteContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ComicSortDBSQLiteContext>();

            // Define a simple connection string for the design-time tools to use.
            // This is just for schema generation; your runtime code uses a different path.
            optionsBuilder.UseSqlite("Data Source=designTime.db");

            return new ComicSortDBSQLiteContext(optionsBuilder.Options);
        }
    }
}
