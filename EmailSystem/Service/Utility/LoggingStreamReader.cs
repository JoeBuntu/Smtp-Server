using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    public class LoggingStreamReader : StreamReader
    {
        private ILogger _Logger;
        public LoggingStreamReader(Stream stream, ILogger logger)
            : base(stream)
        {
            _Logger = logger;
        }

        public string ReadLineWithLogging(string label)
        {
            string retVal = base.ReadLine();
            _Logger.Log("{0}: {1}", label, retVal);
            return retVal;
        } 
    }
}
