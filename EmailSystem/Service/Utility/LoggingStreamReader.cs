using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    public class LoggingStreamReader : StreamReader
    {
        private ScopedActivity _Activity;
        public LoggingStreamReader(Stream stream, ScopedActivity activity)
            : base(stream)
        {
            _Activity = activity;
        }

        public string ReadLineWithLogging(string label)
        {
            string retVal = base.ReadLine();
            _Activity.Log("{0}: {1}", label, retVal);
            return retVal;
        } 
    }
}
