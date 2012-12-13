using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Mail;
using System.IO;
using System.Net.Security;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates; 

namespace Service
{
    class Program
    {
        static void Main(string[] args)
        {
            Program p = new Program();
            p.Run();
 
            Console.ReadLine();
        }
 
        private void Run()
        {
            Guid activityId = Guid.NewGuid();
            Trace.CorrelationManager.ActivityId = activityId;
            Utility.TraceSource.TraceEvent(TraceEventType.Start, 1, "Starting Application");
            
            SmtpListener listener = new SmtpListener();
            listener.ServerCertificate = LoadCertificate();
            listener.Start(); 
        }

        private X509Certificate LoadCertificate()
        {
            X509Certificate retVal = null;
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var collection = store.Certificates.Find(X509FindType.FindBySubjectName, "localhost", true);
                if (collection != null)
                {
                    retVal = collection[0];
                }
            }
            finally
            {
                store.Close();
            }
            return retVal;
        }
 
    }
}
