using ComicSort.Core;
using ComicSort.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.DataAccess
{
    public class ComicDBContext : DbContext
    {
        
        public DbSet<ComicBook> ComicBooks { get; set; }
        private string _connectionString;

        public string CreateConnectionString(string fileName, string filePath)
        {
            string fileNameWithExtension = null;
            _connectionString = null;

            fileNameWithExtension = fileName + ".DB";
            _connectionString = Path.Combine(filePath, fileNameWithExtension);

            return _connectionString;
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source = {_connectionString}");
            base.OnConfiguring(optionsBuilder);
        }

        
    }
}
