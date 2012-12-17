using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Service
{
    [Serializable]
    public class MailPackage
    {
        public MailPackage()
        {
            Tos = new List<string>();
        }

        public string Host { get; set; }
        public DateTime Received { get; set; }
        public string From { get; set; }
        public List<string> Tos { get; set; }
        public Guid ReferenceId { get; set; }
    }
}
