using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Service
{
    public class DefaultLogger : ILogger
    {
        public void Log(string format, params object[] arguments)
        {
            Trace.WriteLine(string.Format(format, arguments));
        }

        public void LogException(Exception ex, string format, params object[] arguments)
        {
            Trace.WriteLine(string.Format(format, arguments));
            Trace.WriteLine(ex.ToString());
        }
    }
}
