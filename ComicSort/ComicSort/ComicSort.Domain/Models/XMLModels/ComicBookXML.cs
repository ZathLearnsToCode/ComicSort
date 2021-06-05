using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ComicSort.Domain.Models.XMLModels
{
    [Serializable]
    [XmlRoot("Book")]
    public class ComicBookXML
    {
        [XmlAttribute("ID")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [XmlAttribute("File")]
        public string FullPath { get; set; }
        [XmlElement]
        public string Series { get; set; }
        [XmlElement]
        public string IssueNumber { get; set; }
        [XmlElement]
        public string Volume { get; set; }
        [XmlElement]
        public int PageCount { get; set; }
        [XmlElement("Added")]
        public string DateAdded { get; set; }
        [XmlElement]
        public long FileSize { get; set; }
        [XmlElement("FileModifiedTime")]
        public string DateModified { get; set; }
        [XmlElement("FileCreationTime")]
        public string DateCreated { get; set; }
    }
}
