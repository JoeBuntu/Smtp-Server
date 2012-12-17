using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service 
{
    public interface IDataStreamRepository
    {
        void Add(Stream data, string uniqueIdentifier);      
    }
}
