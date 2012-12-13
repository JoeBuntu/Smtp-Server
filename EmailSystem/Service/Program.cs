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
            //set up tracing
            Guid activityId = Guid.NewGuid();
            Trace.CorrelationManager.ActivityId = activityId;

            //begin
            Utility.TraceSource.TraceEvent(TraceEventType.Start, 0, "Application Starting");
            try
            {
                Program p = new Program();
                p.Run();

                Console.ReadLine();
            }
            finally
            {
                Utility.TraceSource.TraceEvent(TraceEventType.Stop, 0, "Application Stopping");
            }           
        }
 
        private void Run()
        { 
            SmtpListener listener = new SmtpListener();
            listener.ServerCertificate = LoadCertificate();
            listener.Start(); 
        }

        private X509Certificate LoadCertificate()
        {
            Utility.TraceSource.TraceEvent(TraceEventType.Information, 0, "Loading Certificate");

            X509Certificate retVal = null;
            X509Store store = null; 
            try
            {
                store = new X509Store(StoreName.My, StoreLocation.LocalMachine);         
                store.Open(OpenFlags.ReadOnly);
                var collection = store.Certificates.Find(X509FindType.FindBySubjectName, "localhost", true);
                if (collection != null)
                {
                    retVal = collection[0];
                    Utility.TraceSource.TraceInformation("Certificate Loaded");
                    Utility.TraceSource.TraceData(TraceEventType.Information, 0, retVal);
                }
            }
            catch(Exception ex)
            {
                Utility.TraceSource.TraceInformation("An exception occurred while loading X509 certificate, SSL security is not enabled");
                Utility.TraceSource.TraceData(TraceEventType.Error, 0, ex);
                throw;
            }
            finally
            {
                if (store != null)
                {
                    store.Close();
                }
                
            }
            return retVal;
        }
 
    }
}
