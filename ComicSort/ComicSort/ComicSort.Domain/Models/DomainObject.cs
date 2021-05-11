using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Domain.Models
{
    public class DomainObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}
