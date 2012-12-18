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
    public class SmtpListener : IDisposable
    {
        private bool _HasStarted = false;
        private ScopedActivity _Activity;
        private List<TcpListener> _Listeners = new List<TcpListener>();
        private Dictionary<TcpListener, ListenerInfo> _ListenerInfos = new Dictionary<TcpListener, ListenerInfo>();         
 
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
            try
            {
                _HasStarted = true;
                _Activity = new ScopedActivity("SmtpListener");
                

                InitializeListeners();
            }
            catch (Exception ex)
            {
                if (_Activity != null)
                {
                    _Activity.LogException(ex, "Exception Occurred While Starting Smtp Listener");                    
                }
            }        
        }

        private void InitializeListeners()
        {
            _Activity.Log("Initializing Listeners"); 

            int[] allPorts = NonSecurePorts.Concat(SecurePorts).ToArray();
            for (int i = 0; i < allPorts.Length; i++)
            {
                try
                {
                    //create listener
                    int port = allPorts[i];
                    SmtpSecurity securityMode = DetermineSecurityMode(port);
                    _Activity.Log("Listener[{0}]: Address: {2} Port: {3} Security: {1}", i + 1, securityMode, ListenAddress, port); 
 
                    TcpListener listener = new TcpListener(this.ListenAddress, port);
                    listener.Start();
                    _Activity.Log("Listener[{0}]: Started", i + 1);

                    //prepare collections for listener and listener clients
                    _Listeners.Add(listener); 
                    _ListenerInfos.Add(listener, new ListenerInfo(securityMode, port));

                    listener.BeginAcceptTcpClient(AcceptClient, listener);
                    _Activity.Log("Listener[{0}]: now waiting for client connections", i + 1);
                }
                catch (Exception ex)
                {
                    _Activity.LogException(ex, "An exception occurred while initializing Listener[{0}]", i + 1); 
                }
            }
            _Activity.Log("Finished Initializing Listeners");
        }

        private void AcceptClient(IAsyncResult result)
        { 
            using(ScopedActivity localActivity = new ScopedActivity("Accepting Client Connection"))
            {
                TcpClient client = null;
                TcpListener listener = (TcpListener)result.AsyncState;

                //this section is critical for the robustness of the application.
                //it should only accept the client and begin accepting new connections
                //in the finally block
                try
                {
                    localActivity.Log("Listener: {0}", listener.LocalEndpoint);
                    client = listener.EndAcceptTcpClient(result);
                }
                catch (Exception ex)
                {
                    localActivity.LogException(ex, "An exception occurred while accepting client connection");
                }
                finally
                {
                    listener.BeginAcceptTcpClient(AcceptClient, listener);
                }

                //if client was successfully accepted, continue the processing chain
                if (client != null)
                {
                    ProcessClient(client, listener, localActivity);
                }
            }
        }

        private void ProcessClient(TcpClient client, TcpListener listener, ScopedActivity activity)
        {
            ListenerInfo info = _ListenerInfos[listener];
            try
            { 
                info.Clients.Add(client);

                //gather some metrics...
                activity.Log("Client: {0}", client.Client.RemoteEndPoint);
                activity.Log("Active Connections for Listener: {0}", info.Clients.Count);

                //run validation checks...
                if (ValidateClient(client, activity))
                {
                    //process client
                    activity.Log("Exchanging Commands");
                    using (SmtpStreamProcessor clientProcessor = new SmtpStreamProcessor(client.GetStream(), info.SecurityMode, ServerCertificate))
                    {
                        clientProcessor.Process();
                    }
                }
            }
            finally
            {
                info.Clients.Remove(client);
                client.Close();
            }
        }

        private bool ValidateClient(TcpClient client, ScopedActivity activity)
        {
            bool retVal = false;
            activity.Log("Checking if client IP Address is black listed");
            IPEndPoint clientEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;
            if (!BlackListedIpAddresses.Contains(clientEndpoint.Address))
            {
                //address is not black listed
                activity.Log("Address {0} is allowed, client passes validation checks", clientEndpoint.Address);
                retVal = true;
            }
            else
            {
                //address is black listed
                activity.Log("Not Processing Client, Client IP Address is black listed: {0}", clientEndpoint.Address);
            }
            return retVal;
        }

        #endregion

        #region Helpers

        private class ListenerInfo
        {
            public ListenerInfo(SmtpSecurity securityMode, int port)
            {
                Clients = new List<TcpClient>();
                SecurityMode = securityMode;
                Port = port;
            }

            public List<TcpClient> Clients { get; private set; }
            public SmtpSecurity SecurityMode { get; private set; }
            public int Port { get; private set; }
        }
 
        private SmtpSecurity DetermineSecurityMode(int port)
        {
            SmtpSecurity retVal;
            if (NonSecurePorts.Contains(port))
            {
                retVal = SmtpSecurity.STARTTLS;
            }
            else
            {
                retVal = SmtpSecurity.SSL;
            }
            return retVal;
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
 
        public void Dispose()
        {
            foreach (TcpListener listener in _Listeners)
            {
                if (_ListenerInfos != null)
                {
                    ListenerInfo info = _ListenerInfos[listener];
                    if (info.Clients != null)
                    {
                        foreach (TcpClient client in info.Clients)
                        {
                            client.Close();
                        }
                    }
                }
                listener.Stop();
            }
            _Activity.Dispose();
        }

    }
}
