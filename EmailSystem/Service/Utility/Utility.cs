using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Service 
{
    public class Utility
    {
        public static TraceSource CreateTraceSource()
        {
            return new TraceSource("EmailingSystem.Service", SourceLevels.All);
        }
    }
}
