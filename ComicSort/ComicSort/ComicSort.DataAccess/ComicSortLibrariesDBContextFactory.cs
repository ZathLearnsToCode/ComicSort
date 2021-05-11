using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.DataAccess
{
    public class ComicSortLibrariesDBContextFactory
    {
        private readonly Action<DbContextOptionsBuilder> _configureDbContext;

        public ComicSortLibrariesDBContextFactory(Action<DbContextOptionsBuilder> configureDbContext)
        {
            _configureDbContext = configureDbContext;
        }

        public ComicSortLibrariesDBContext CreateDbContext()
        {
            DbContextOptionsBuilder<ComicSortLibrariesDBContext> options = new DbContextOptionsBuilder<ComicSortLibrariesDBContext>();
            _configureDbContext(options);

            return new ComicSortLibrariesDBContext(options.Options);
        }
    }
}
