using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    public class LoggingStreamWriter : StreamWriter
    {
        private ILogger _Logger;
        public LoggingStreamWriter(Stream stream, ILogger logger)
            : base(stream)
        {
            _Logger = logger;
        }

        public void WriteLineWithLogging(string line, string label)
        {
            _Logger.Log("{0}: {1}", label, line);
            base.WriteLine(line);
        }
    }
}
