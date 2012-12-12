using System;
using System.Collections.Generic; 
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO; 
using System.Security.Cryptography.X509Certificates;

namespace Service
{
    public class SmtpListener
    {
        private bool _HasStarted = false;
        private List<TcpListener> _Listeners = new List<TcpListener>();
        private Dictionary<TcpListener, List<TcpClient>> _ListenerClients = new Dictionary<TcpListener, List<TcpClient>>();
 
        #region Properties

        public int[] NonSecurePorts
        {
            get
            {
                return _NonSecurePorts.ToArray();
            }
            set
            {
                CheckForNullArgument("NonSecurePorts", value);
                CheckIfConfigurationMayBeChanged();
                _NonSecurePorts = value.ToArray();
            }
        }
        private int[] _NonSecurePorts = new int[] { 25, 587 };

        public int[] SecurePorts
        {
            get
            {
                return _SecurePorts.ToArray();
            }
            set
            {
                CheckForNullArgument("SecurePorts", value);
                CheckIfConfigurationMayBeChanged();
                _SecurePorts = value.ToArray();
            }
        }
        private int[] _SecurePorts = new int[] { 465 };

        public IPAddress[] BlackListedIpAddresses
        {
            get
            {
                return _BlackListedIpAddresses.ToArray();
            }
            set
            {
                CheckForNullArgument("SecurePorts", value);
                CheckIfConfigurationMayBeChanged();
                _BlackListedIpAddresses = value.ToArray(); 
            }
        }
        private IPAddress[] _BlackListedIpAddresses = new IPAddress[] { };

        public IPAddress ListenAddress
        {
            get { return _ListenAddress; }
            set
            {
                CheckForNullArgument("ListenAddress", value);
                CheckIfConfigurationMayBeChanged();
                _ListenAddress = value;
            }
        }
        private IPAddress _ListenAddress = IPAddress.Any;

        public X509Certificate ServerCertificate
        {
            get { return _ServerCertificate; }
            set
            {
                CheckForNullArgument("ServerCertificate", value);
                CheckIfConfigurationMayBeChanged();
                _ServerCertificate = value;
            }
        }
        private X509Certificate _ServerCertificate = null;

        #endregion

        #region Core

        public void Start()
        {
            _HasStarted = true;

            using (new SectionLogger("Starting"))
            {
                using (new SectionLogger("Creating NonSecure Listeners"))
                {
                    InitializeListenerGroup("NonSecure_Listener", NonSecurePorts, AcceptNonSecureClient);
                }
                using (new SectionLogger("Creating Secure Listeners"))
                {
                    InitializeListenerGroup("Secure_Listener", SecurePorts, AcceptSecureClient);
                }
            } 
        }

        private void InitializeListenerGroup(string groupName, int[] ports, AsyncCallback clientAcceptCallback)
        {
            for (int i = 0; i < ports.Length; i++)
            {
                try
                {
                    //create listener
                    int port = ports[i];
                    Log("Initializing {0}_{1}: Address: {2} Port: {3}", groupName, i + 1, ListenAddress, port);
                    TcpListener listener = new TcpListener(this.ListenAddress, port);

                    //start listener
                    listener.Start();
                    Log("Started {0}_{1}", groupName, i + 1);

                    //prepare collections for listener and listener clients
                    _Listeners.Add(listener);
                    _ListenerClients.Add(listener, new List<TcpClient>());

                    listener.BeginAcceptTcpClient(clientAcceptCallback, listener);
                    Log("{0}_{1} now waiting for client connections", groupName, i + 1);
                }
                catch (Exception ex)
                {
                    LogException(ex, "An exception occurred while initializing {0}_{1}", groupName, i + 1);
                }
            }
        }

        private void AcceptNonSecureClient(IAsyncResult result)
        {
            TcpClient client = null;
            TcpListener listener = (TcpListener)result.AsyncState;

            using (new SectionLogger("Accepting NonSecure Client Connection"))
            { 
                //this section is critical for the robustness of the application.
                //it should only accept the client and begin accepting new connections
                //in the finally block
                try
                {
                    Log("Listener: {0}", listener.LocalEndpoint);
                    client = listener.EndAcceptTcpClient(result);
                }
                catch (Exception ex)
                {
                    LogException(ex, "An exception occurred while accepting client connection");
                }
                finally
                {
                    listener.BeginAcceptTcpClient(AcceptNonSecureClient, listener);
                }
            }

            //if client was successfully accepted, continue the processing chain
            if (client != null)
            {
                ProcessClient(client, listener, SmtpSecurity.STARTTLS);
            }
        }

        private void AcceptSecureClient(IAsyncResult result)
        {
            TcpClient client = null;
            TcpListener listener = (TcpListener)result.AsyncState;

            using (new SectionLogger("Accepting Secure Client Connection"))
            {
                //this section is critical for the robustness of the application.
                //it should only accept the client and begin accepting new connections
                //in the finally block
                try
                {
                    Log("Listener: {0}", listener.LocalEndpoint);
                    client = listener.EndAcceptTcpClient(result);
                }
                catch (Exception ex)
                {
                    LogException(ex, "An exception occurred while accepting client connection");
                }
                finally
                {
                    listener.BeginAcceptTcpClient(AcceptNonSecureClient, listener);
                }
            }

            //if client was successfully accepted, continue the processing chain
            if (client != null)
            { 
                ProcessClient(client, listener, SmtpSecurity.SSL);
            }
        }

        private void ProcessClient(TcpClient client, TcpListener listener, SmtpSecurity securityMode)
        { 
            using (new SectionLogger("Processing Client"))
            {
                List<TcpClient> activeClients = null;
                try
                {
                    //gather some metrics...
                    activeClients = _ListenerClients[listener];
                    activeClients.Add(client);

                    Log("Client: {0}", client.Client.RemoteEndPoint);
                    Log("Active Connections for Listener: {0}", activeClients.Count);

                    //run validation checks...
                    if (ValidateClient(client))
                    {
                        //process client
                        using (new SectionLogger("Exchanging Commands"))
                        {
                            using (SmtpClientProcessor clientProcessor = new SmtpClientProcessor(client, securityMode, ServerCertificate, Kernel.Get<ILogger>()))
                            {
                                clientProcessor.Process();
                            }
                        }
                    }
                }
                finally
                {
                    activeClients.Remove(client);
                    client.Close();
                }
            }
        }

        private bool ValidateClient(TcpClient client)
        {
            bool retVal = false;
            using (new SectionLogger("Client Validation"))
            {
                Log("Checking if client IP Address is black listed");
                IPEndPoint clientEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;
                if (!BlackListedIpAddresses.Contains(clientEndpoint.Address))
                {
                    //address is not black listed
                    Log("Address {0} is allowed, client passes validation checks", clientEndpoint.Address);
                    retVal = true;
                }
                else
                {
                    //address is black listed
                    Log("Not Processing Client, Client IP Address is black listed: {0}", clientEndpoint.Address);
                }
            }
            return retVal;
        }

        #endregion

        #region Helpers

        private class SectionLogger : IDisposable
        {
            public SectionLogger(string sectionTitle)
            {
                Trace.WriteLine(string.Empty);
                Trace.WriteLine(sectionTitle + ":");
                Trace.Indent();
            }
 
            public void Dispose()
            {
                Trace.Unindent();
            }
        }

        private void Log(string format, params object[] arguments)
        {
            Trace.WriteLine(string.Format(format, arguments));
        }

        private void LogException(Exception ex, string format, params object[] arguments)
        {
            Trace.WriteLine(string.Format(format, arguments));
            Trace.WriteLine(ex.ToString());
        }

        private void CheckForNullArgument(string name, object argument)
        {
            if (argument == null)
                throw new ArgumentNullException(name);
        }

        private void CheckIfConfigurationMayBeChanged()
        {
            if (_HasStarted)
                throw new InvalidOperationException("Listener configuration changes are not allowed after it has been started");
        }

        #endregion
 
    }
}
