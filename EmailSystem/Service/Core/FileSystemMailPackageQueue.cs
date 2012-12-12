using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;

namespace Service
{
    public class FileSystemMailPackageQueue : IMailPackageQueue
    {
        public void Add(MailPackage package)
        {
            using (FileStream fs = File.OpenWrite(string.Format("package.{0:MMddyyyyHHmmssfffff}.txt", DateTime.Now)))
            {
                DataContractSerializer ds = new DataContractSerializer(typeof(MailPackage));
                ds.WriteObject(fs, package);               
            }
        }
    }
}
