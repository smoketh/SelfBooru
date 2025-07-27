using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
namespace SelfBooru
{
    internal class TaggedDiskImage
    {
        public ObjectId Id { get; set; }
        
        public required string md5Hash { get; set; }
        public required string filePath { get; set; }
        //public required byte[] thumbnail { get; set; }
        public required int filesize { get; set; }
        public DateTime created { get; set; }
        public DateTime lastSeen { get; set; }
        public required string metadata { get; set; }

        public List<ObjectId>? tags { get; set; } = new List<ObjectId>();

    }
    internal class Tag
    {
        public ObjectId Id { get; set;}
        public required string text { get; set; }


    }
}
