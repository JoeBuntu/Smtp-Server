using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    public class FileSystemStreamRepository : IDataStreamRepository
    {
        public void Add(Stream data, string uniqueIdentifier)
        {
            using (FileStream fs = File.OpenWrite(uniqueIdentifier + ".data"))
            {
                data.CopyTo(fs);
            }
        }
    }
}
