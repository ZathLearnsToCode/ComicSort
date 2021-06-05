using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ComicSort.Domain.Models.XMLModels
{
    [Serializable]
    public class ComicDatabaseXml
    {
        [XmlAttribute("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [XmlElement("Books")]
        public List<ComicBookXML> Books { get; set; }

        public ComicDatabaseXml()
        {
            
        }
    }
}
