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
            //Console.ReadLine();
            //SendEmail();
            //Console.ReadLine();
            SendEmail();
            Console.ReadLine();
        }

        private readonly Encoding _Encoder;
        public Program()
        {
            _Encoder = Encoding.ASCII;
        }

        private static void SendEmail()
        {
            SmtpClient client = new SmtpClient(IPAddress.Loopback.ToString(), 465);
            client.EnableSsl = true; 

            MailMessage message = new MailMessage();
            message.From = new MailAddress("jcooper@email.com", "Dr Cooper");
            message.To.Add(new MailAddress("1@email.com", "Number 1"));
            message.To.Add(new MailAddress("2@email.com", "Number 2"));
            message.To.Add(new MailAddress("3@email.com", "Number 3"));
            message.To.Add(new MailAddress("4@email.com", "Number 4"));
            message.CC.Add(new MailAddress("5@email.com", "Number 5"));
            message.CC.Add(new MailAddress("6@email.com", "Number 6"));
            message.CC.Add(new MailAddress("7@email.com", "Number 7"));
            message.CC.Add(new MailAddress("7@email.com", "Number 8"));
            message.Subject = "This is my subject";
            message.Body = ".";


            Attachment attachment = new Attachment(File.Open(@"C:\Users\Joe\Documents\WebCam Media\Capture\ArcSoft_Video14.wmv", FileMode.Open), "john", "video/x-ms-wmv");
            message.Attachments.Add(attachment);

            System.Net.ServicePointManager.ServerCertificateValidationCallback = Callback;
            client.Send(message);
            attachment.Dispose();
        }

        private static bool Callback(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        private List<TcpListener> _Listeners = new List<TcpListener>();
        private Dictionary<TcpListener, List<TcpClient>> _ListenerClients = new Dictionary<TcpListener, List<TcpClient>>();

        private void Run()
        {
            
            X509Certificate certificate = null;
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var collection = store.Certificates.Find(X509FindType.FindBySubjectName, "localhost", true);
                if (collection != null)
                {
                    certificate = collection[0];
                }
            }
            finally
            {
                store.Close();
            }

            
            SmtpListener listener = new SmtpListener();
            listener.ServerCertificate = certificate;
            listener.Start();

            ////start non-secure listeners            
            //int[] nonSecurePorts = new int[] { 25, 587};

            //foreach (int port in nonSecurePorts)
            //{
            //    TcpListener listener = new TcpListener(IPAddress.Any, port);
            //    listener.Start();
            //    _Listeners.Add(listener);
            //    _ListenerClients.Add(listener, new List<TcpClient>());
            //    listener.BeginAcceptTcpClient(AcceptClient, listener);       
            //}

            ////start secure listeners
            //int[] securePorts = new int[] { 465 };
            //foreach (int port in securePorts)
            //{
            //    TcpListener listener = new TcpListener(IPAddress.Any, port);
            //    listener.Start();
            //    _Listeners.Add(listener);
            //    _ListenerClients.Add(listener, new List<TcpClient>());
            //    listener.BeginAcceptTcpClient(AcceptClient, listener);
            //}
        }

        private void AcceptClient(IAsyncResult result)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);
 
            _ListenerClients[listener].Add(client);
            Trace.WriteLine(string.Format("Listener {0} accepted connection from client {1}", listener.LocalEndpoint, client.Client.RemoteEndPoint));
            Trace.WriteLine(string.Format("Listener {0} active connections: {1}", listener.LocalEndpoint, _ListenerClients[listener].Count));


            listener.BeginAcceptTcpClient(AcceptClient, listener);
            ProcessClient(client);
        }

        private void ProcessClient(TcpClient client)
        {

 
            StreamReader reader = new StreamReader(client.GetStream());
            StreamWriter writer = new StreamWriter(client.GetStream());
            writer.NewLine = "\r\n";
            writer.AutoFlush = true;

            try
            {
                writer.WriteLine("220 localhost -- Fake proxy server");
 
                while (true)
                {
                    string response = null;
                    string line = reader.ReadLine();
                    Console.WriteLine("c: {0}", line);

                    if (line.StartsWith("EHLO"))
                    {
                        response = "250 OK";
                        writer.WriteLine(response);
                        writer.Flush();
                        Console.WriteLine("s: {0}", response);
                    }
                    if (line.StartsWith("MAIL FROM"))
                    {
                        response = "250 OK";
                        writer.WriteLine(response);
                        writer.Flush();
                        Console.WriteLine("s: {0}", response);
                    }
                    if (line.StartsWith("RCPT TO"))
                    {
                        response = "250 OK";

                        writer.WriteLine(response);
                        writer.Flush();
                        Console.WriteLine("s: {0}", response);
                    }
                    if (line.StartsWith("DATA"))
                    {
                        response = "354 Start mail input; end with";
                        writer.WriteLine(response);
                        writer.Flush();
                        Console.WriteLine("s: {0}", response);

                        using (StreamWriter bob = new StreamWriter("email.txt"))
                        {
                            while ((line = reader.ReadLine()) != null)
                            {
                                Console.WriteLine("c: {0}", line);
                                if (line.Equals("."))
                                {
                                    break;
                                }
                                bob.WriteLine(line);
                            }
                        }

                        response = "250 Ok: queued as 12345";
                        writer.WriteLine(response);
                        writer.Flush();
                        Console.WriteLine("s: {0}", response);
                    }
                    if (line.StartsWith("QUIT"))
                    {
                        response = "221 Bye";
                        writer.WriteLine(response);
                        writer.Flush();
                        Console.WriteLine("s: {0}", response);
                        break;
                    }
                }
            }
            finally
            {
                reader.Dispose();
                writer.Dispose();
                client.Close();
            }
 
        }

    }
}
