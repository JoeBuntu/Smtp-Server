using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Service
{
    public interface ILogger
    {
        void Log(string format, params object[] arguments);

        void LogException(Exception ex, string format, params object[] arguments);
    }
}
