using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    public class LoggingStreamWriter : StreamWriter
    {
        private ScopedActivity _Activity;
        public LoggingStreamWriter(Stream stream, ScopedActivity activity)
            : base(stream)
        {
            _Activity = activity;
        }

        public void WriteLineWithLogging(string line, string label)
        {
            _Activity.Log("{0}: {1}", label, line);
            base.WriteLine(line);
        }
    }
}
