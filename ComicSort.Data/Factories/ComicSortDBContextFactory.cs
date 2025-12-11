using ComicSort.Data.SQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;
using SQLitePCL;

namespace ComicSort.Data.Factories
{
    public class ComicSortDBContextFactory : IDesignTimeDbContextFactory<ComicSortDBSQLiteContext>
    {
        public ComicSortDBSQLiteContext CreateDbContext(string[] args)
        {
            // Fix for SQLitePCL initialization
            Batteries_V2.Init();

            var optionsBuilder = new DbContextOptionsBuilder<ComicSortDBSQLiteContext>();

            string dbFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ComicSort");

            Directory.CreateDirectory(dbFolder);

            string dbPath = Path.Combine(dbFolder, "ComicSort.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new ComicSortDBSQLiteContext(optionsBuilder.Options);
        }
    }
}
