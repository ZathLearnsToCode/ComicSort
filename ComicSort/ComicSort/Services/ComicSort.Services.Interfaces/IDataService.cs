using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Services.Interfaces
{
    public interface IDataService<T>
    {
        Task<IEnumerable<T>> GetAll();

        Task<T> Get(Guid id);

        Task<T> Create(T entity);

        Task<T> Update(Guid id, T entity);

        Task<bool> Delete(Guid id);
    }
}
