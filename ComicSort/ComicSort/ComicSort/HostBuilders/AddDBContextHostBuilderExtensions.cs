using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComicSort.DataAccess;

namespace ComicSort.HostBuilders
{
    public static class AddDBContextHostBuilderExtensions
    {
        public static IHostBuilder AddDBContext(this IHostBuilder host)
        {
            host.ConfigureServices((context, services) =>
            {
                string connectionString = context.Configuration.GetConnectionString("SQLite_ComicSortLibraries");
                Action<DbContextOptionsBuilder> configureDBContext = o => o.UseSqlite(connectionString);

                services.AddDbContext<ComicSortLibrariesDBContext>(configureDBContext);
                services.AddSingleton<ComicSortLibrariesDBContextFactory>(new ComicSortLibrariesDBContextFactory(configureDBContext));
            });

            return host;
        }
    }
}
