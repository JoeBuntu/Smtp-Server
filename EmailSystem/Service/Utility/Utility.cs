using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Service 
{
    public class Utility
    {
        public static readonly TraceSource TraceSource = new TraceSource("EmailingSystem.Service", SourceLevels.Warning);
    }
}
