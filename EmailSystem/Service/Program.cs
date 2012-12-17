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
using System.Runtime.Serialization;
using System.Xml;

namespace Service
{
    public class Program
    {
        private static ScopedActivity _Activity;
        private static SmtpListener _Listener;

        static void Main(string[] args)
        {
            _Activity = new ScopedActivity("Application");
            try
            {
                Program p = new Program();
                p.Run();

                Console.ReadLine();
            }
            finally
            {
                if (_Listener != null)
                {
                    _Listener.Dispose();
                } 
                _Activity.Dispose();
            }
        }

        private void Run()
        {
            _Listener = new SmtpListener();
            _Listener.ServerCertificate = LoadCertificate();
            _Listener.Start();
        }

        private X509Certificate LoadCertificate()
        {
            _Activity.Log("Loading Certificate"); 

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

                    _Activity.Log("Certificate Loaded");
                    _Activity.TraceSource.TraceData(TraceEventType.Information, 0, retVal);
                }
            }
            catch (Exception ex)
            {
                _Activity.LogException(ex, "An exception occurred while loading X509 certificate, SSL security is not enabled");
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
